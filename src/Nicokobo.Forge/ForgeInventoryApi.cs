using Il2Cpp;
using Nicokobo.Forge.Runtime;
using System.Collections.ObjectModel;

namespace Nicokobo.Forge;

public enum NativeInventorySnapshotStatus
{
    Ready, Disabled, Unavailable, Invalid
}

public sealed record NativeInventoryItem(int InstanceId, string ItemId,
    int Units, bool PlayerOwned);

public sealed record NativeInventorySnapshot(NativeInventorySnapshotStatus Status,
    IReadOnlyList<NativeInventoryItem>? Items = null, string? Reason = null);

public enum NativeTransferPreviewStatus
{
    Ready, Disabled, Unavailable, Invalid, SourceMismatch, Locked,
    NoWholeSlot
}

public sealed record NativeTransferPreview(NativeTransferPreviewStatus Status,
    int ItemInstanceId = 0, int Units = 0, string? Reason = null);

/// <summary>Build-gated direct-child inventory observation. No native inventory
/// mutation or recursive container traversal is performed.</summary>
public static class ForgeInventoryApi
{
    private const int MaxDirectItems = ForgeNumbers.Inventory.MaxDirectItems;
    private static bool _enabled;
    private static bool _previewEnabled;

    internal static void SetEnabled(bool readEnabled, bool previewEnabled)
    {
        _enabled = readEnabled;
        _previewEnabled = readEnabled && previewEnabled;
    }

    /// <summary>A detached list of live handles from the current run. Includes
    /// nested inventories and machine modules. Capture at an event boundary,
    /// then revalidate ownership and parent inventory before a mutation.</summary>
    public static IReadOnlyList<GameItem> CaptureRunItems(PlayerStore store)
    {
        if (!_enabled) throw new InvalidOperationException("Run inventory adapter unavailable");
        if (store == null || store.Pointer == IntPtr.Zero) throw new ArgumentException("Current store is required");
        var result = new List<GameItem>();
        var seen = new HashSet<IntPtr>();
        var pending = new Queue<GameItem>();
        void Add(GameItem? item)
        {
            if (item == null || item.Pointer == IntPtr.Zero || !seen.Add(item.Pointer)) return;
            if (seen.Count > ForgeNumbers.Inventory.MaxRunItems) throw new InvalidOperationException("Run item traversal exceeds limit");
            pending.Enqueue(item);
        }
        var all = store.FindAllItem(true) ?? throw new InvalidOperationException("Run items unavailable");
        for (int i = 0; i < all.Count; i++) Add(all[i]);
        if (store.saveBags != null) foreach (var bag in store.saveBags.Values) Add(bag);
        if (store.gridInv?.childItems != null) foreach (var item in store.gridInv.childItems) Add(item);
        while (pending.TryDequeue(out var item))
        {
            result.Add(item);
            if (item.children != null)
            {
                if (item.children.Count > ForgeNumbers.Inventory.MaxChildrenPerItem) throw new InvalidOperationException("Item child traversal exceeds limit");
                foreach (var child in item.children)
                {
                    GameInventory? inventory;
                    try { inventory = child.Cast<GameInventory>(); } catch (InvalidCastException) { continue; }
                    if (inventory?.childItems != null) foreach (var nested in inventory.childItems) Add(nested);
                }
            }
            GameGridInventory? modules;
            try { modules = ForgeModuleApi.GetInventory(item); }
            catch { modules = null; } // Non-machine native items may not expose a bay.
            if (modules?.items != null) foreach (var module in modules.items) Add(module);
        }
        return result.AsReadOnly();
    }

    public static bool IsPlayerOwned(GameItem item) => _enabled && item != null &&
        item.Pointer != IntPtr.Zero && GeneralHelper.IsItemOwned(item);

    /// <summary>Detached positive unit counts by item ID for read-only effects.
    /// Reads share one snapshot within a frame, until native inventory, quantity,
    /// ownership or run changes. Pass relevant IDs to limit native ownership checks;
    /// the map may also include IDs requested by other readers. Transaction
    /// preflight must use a fresh capture.</summary>
    public static IReadOnlyDictionary<string, int> CaptureOwnedItemCounts(PlayerStore store,
        IReadOnlyCollection<string>? relevantIds = null)
    {
        if (!_enabled) throw new InvalidOperationException("Run inventory adapter unavailable");
        if (store == null || store.Pointer == IntPtr.Zero ||
            string.IsNullOrWhiteSpace(store.runID) || store.saveSlotId < 0)
            throw new ArgumentException("Current run is required");
        return InventoryReadRuntime.Capture(store, relevantIds, ids =>
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in CaptureRunItems(store))
            {
                int units = item.unitCount;
                if (units <= 0) continue;
                string id = item.identifier;
                if (string.IsNullOrWhiteSpace(id) || ids != null && !ids.Contains(id) || !IsPlayerOwned(item)) continue;
                counts.TryGetValue(id, out int previous);
                counts[id] = (int)Math.Min(int.MaxValue, (long)previous + units);
            }
            return new ReadOnlyDictionary<string, int>(counts);
        });
    }

    /// <summary>Observes the existing native drag handler without creating one.</summary>
    public static bool IsItemDragActive
    {
        get
        {
            if (!_enabled) return false;
            var handler = ItemMouseDragHandler.current;
            return handler != null && handler.Pointer != IntPtr.Zero && handler.IsDraggingItem;
        }
    }

    public static NativeInventorySnapshot CaptureDirect(GameInventory? inventory)
    {
        if (!_enabled) return new(NativeInventorySnapshotStatus.Disabled);
        if (inventory == null || inventory.Pointer == IntPtr.Zero)
            return new(NativeInventorySnapshotStatus.Unavailable);
        try
        {
            var children = inventory.childItems;
            if (children == null || children.Count > MaxDirectItems)
                return new(NativeInventorySnapshotStatus.Unavailable,
                    Reason: "Direct child list unavailable or exceeds limit");
            var seen = new HashSet<int>();
            var result = new List<NativeInventoryItem>(children.Count);
            for (int index = 0; index < children.Count; index++)
            {
                var item = children[index];
                if (item == null || item.Pointer == IntPtr.Zero ||
                    string.IsNullOrWhiteSpace(item.identifier) || item.uniqueId == 0 ||
                    item.unitCount < 0 || !seen.Add(item.uniqueId) ||
                    item.parentInventory == null ||
                    item.parentInventory.Pointer != inventory.Pointer)
                    return new(NativeInventorySnapshotStatus.Invalid,
                        Reason: $"Invalid direct child at index {index}");
                result.Add(new NativeInventoryItem(item.uniqueId, item.identifier,
                    item.unitCount, item.IsTag("IS_OWNED_TAG")));
            }
            return new(NativeInventorySnapshotStatus.Ready,
                Array.AsReadOnly(result.ToArray()));
        }
        catch (Exception ex)
        {
            return new(NativeInventorySnapshotStatus.Unavailable,
                Reason: ex.GetType().Name);
        }
    }

    /// <summary>Conservative, read-only preflight for one whole item between
    /// grid inventories. Stacking and splitting are rejected. A Ready result
    /// is not permission to write: the complete native transaction is gated off.</summary>
    public static NativeTransferPreview PreviewWholeGridTransfer(GameItem? item,
        GameGridInventory? source, GameGridInventory? destination)
    {
        if (!_previewEnabled) return new(NativeTransferPreviewStatus.Disabled);
        if (item == null || source == null || destination == null ||
            item.Pointer == IntPtr.Zero || source.Pointer == IntPtr.Zero ||
            destination.Pointer == IntPtr.Zero)
            return new(NativeTransferPreviewStatus.Unavailable);
        if (source.Pointer == destination.Pointer)
            return new(NativeTransferPreviewStatus.Invalid,
                Reason: "Source and destination are identical");
        try
        {
            var beforeSource = CaptureDirect(source);
            var beforeDestination = CaptureDirect(destination);
            if (beforeSource.Status != NativeInventorySnapshotStatus.Ready ||
                beforeDestination.Status != NativeInventorySnapshotStatus.Ready)
                return new(NativeTransferPreviewStatus.Unavailable,
                    Reason: "Inventory snapshot unavailable");
            int id = item.uniqueId;
            int units = item.unitCount;
            if (id <= 0 || units <= 0 || string.IsNullOrWhiteSpace(item.identifier))
                return new(NativeTransferPreviewStatus.Invalid);
            if (item.parentInventory == null ||
                item.parentInventory.Pointer != source.Pointer ||
                !beforeSource.Items!.Any(x => x.InstanceId == id &&
                    x.ItemId == item.identifier && x.Units == units && x.PlayerOwned) ||
                beforeDestination.Items!.Any(x => x.InstanceId == id))
                return new(NativeTransferPreviewStatus.SourceMismatch);
            if (source.IsRemoveLocked() || destination.IsInsertLocked() ||
                item.MaxNumRemove() < units)
                return new(NativeTransferPreviewStatus.Locked);
            var slot = destination.TryFindOneValidInventorySlot(item, false);
            if (slot == null || slot.Pointer == IntPtr.Zero || !slot.IsValid() ||
                slot.item == null || slot.item.Pointer != item.Pointer ||
                slot.inventory == null || slot.inventory.Pointer != destination.Pointer ||
                slot.targetItem != null || slot.numTransfer < units)
                return new(NativeTransferPreviewStatus.NoWholeSlot);
            // Callers must rerun preflight immediately before a future write.
            return new(NativeTransferPreviewStatus.Ready, id, units);
        }
        catch (Exception ex)
        {
            return new(NativeTransferPreviewStatus.Unavailable,
                Reason: ex.GetType().Name);
        }
    }
}
