using Il2Cpp;
using UnityEngine;

namespace Nicokobo.Forge.Runtime;

internal static class InventoryReadRuntime
{
    private static readonly InventoryReadCache<IReadOnlyDictionary<string, int>> Counts =
        new(ForgeNumbers.Inventory.OwnedCountCacheSeconds);
    private static readonly HashSet<string> RequestedIds = new(StringComparer.Ordinal);
    private static bool _allIds;
    private static bool _enabled;

    internal static void Install(bool allowed, Action<string> log)
    {
        _enabled = false;
        RequestedIds.Clear();
        _allIds = false;
        Counts.Invalidate();
        if (!allowed) return;
        var hooks = new List<NativeHook>
        {
            new(typeof(GameItem), nameof(GameItem.SetUnitCount), [typeof(int)], typeof(GameItem),
                typeof(InventoryReadRuntime), Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)),
            new(typeof(GeneralHelper), nameof(GeneralHelper.SetItemOwned), [typeof(GameItem), typeof(bool)], typeof(void),
                typeof(InventoryReadRuntime), Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)),
            new(typeof(GameItemElement), nameof(GameItemElement.Destroy), [], typeof(void),
                typeof(InventoryReadRuntime), Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)),
            new(typeof(GameItem), nameof(GameItem.ModifyTag), [typeof(string), typeof(Il2CppSystem.Action<TagState>), typeof(bool)], typeof(GameItem),
                typeof(InventoryReadRuntime), Prefix: nameof(OwnershipTagChanged), Postfix: nameof(OwnershipTagChanged))
        };
        foreach (string name in new[] { nameof(GameItem.EnableTag), nameof(GameItem.DisableTag) })
            hooks.Add(new(typeof(GameItem), name, [typeof(string), typeof(bool)], typeof(bool),
                typeof(InventoryReadRuntime), Prefix: nameof(OwnershipTagChanged), Postfix: nameof(OwnershipTagChanged)));
        foreach (var type in new[] { typeof(GameGridInventory), typeof(GameSlotInventory),
                     typeof(GameGridScrollableInventory), typeof(GameCharacterRaidInventory) })
            foreach (string name in new[] { nameof(GameInventory.UncheckedAccept), nameof(GameInventory.Expel) })
                hooks.Add(new(type, name, [typeof(GameItem)], typeof(bool), typeof(InventoryReadRuntime),
                    Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)));
        foreach (string name in new[] { nameof(PlayerStore.LoadGame), nameof(PlayerStore.StartNewGame),
                     nameof(PlayerStore.EndNight), nameof(PlayerStore.EndDay) })
            hooks.Add(new(typeof(PlayerStore), name, [], typeof(void), typeof(InventoryReadRuntime),
                Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)));
        foreach (string name in new[] { nameof(PlayerStore.BuyItem), nameof(PlayerStore.SellItem) })
            hooks.Add(new(typeof(PlayerStore), name, [typeof(GameItem)], typeof(void), typeof(InventoryReadRuntime),
                Prefix: nameof(Invalidate), Postfix: nameof(Invalidate)));
        _enabled = NativeHookSet.Install("nicokobo.forge.inventory_read_cache", hooks, log);
        if (!_enabled)
            log("[WARN] [NicokoboForge/Inventory] read coalescing unavailable; using fresh ownership snapshots");
    }

    internal static IReadOnlyDictionary<string, int> Capture(PlayerStore store,
        IReadOnlyCollection<string>? requestedIds, Func<IReadOnlySet<string>?, IReadOnlyDictionary<string, int>> capture)
    {
        if (requestedIds == null)
        {
            if (!_allIds) { _allIds = true; Counts.Invalidate(); }
        }
        else
        {
            foreach (string id in requestedIds)
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A requested item ID is empty");
            bool changed = false;
            foreach (string id in requestedIds) changed |= RequestedIds.Add(id);
            if (changed) Counts.Invalidate();
        }
        IReadOnlyDictionary<string, int> Scan() => capture(_allIds ? null : RequestedIds);
        return !_enabled ? Scan() :
            Counts.Capture(new(store.Pointer, store.runID, store.saveSlotId), Time.unscaledTimeAsDouble, Scan);
    }

    private static void OwnershipTagChanged(string __0)
    {
        if (__0 == "IS_OWNED_TAG") Invalidate();
    }

    private static void Invalidate() => Counts.Invalidate();
}
