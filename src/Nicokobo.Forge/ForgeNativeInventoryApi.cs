using Il2Cpp;

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
public static class ForgeNativeInventoryApi
{
    private const int MaxDirectItems = 4096;
    private static bool _enabled;
    private static bool _previewEnabled;

    internal static void SetEnabled(bool readEnabled, bool previewEnabled)
    {
        _enabled = readEnabled;
        _previewEnabled = readEnabled && previewEnabled;
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
