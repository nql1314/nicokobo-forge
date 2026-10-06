using Il2Cpp;

namespace Nicokobo.Forge.Workshop;

internal static class WorkshopRewardDelivery
{
    internal static void RequireContracts()
    {
        if (!ForgeInventoryPlacementApi.IsAvailable)
            throw new InvalidOperationException("Native inventory placement preview unavailable");
    }

    // Plan the whole grant in a managed bitmap. No reward IDs, shapes or parents
    // change during preflight; native validation still excludes stacking.
    internal static IReadOnlyList<ForgeInventoryPlacement>? Plan(GameGridInventory inventory, IReadOnlyList<GameItem> products) =>
        ForgeInventoryPlacementApi.PlanWholeGrid(inventory, products);

    internal static bool Apply(GameGridInventory inventory, IReadOnlyList<ForgeInventoryPlacement> placements)
    {
        foreach (var placement in placements)
        {
            if (placement.Item == null || placement.Item.Pointer == IntPtr.Zero ||
                placement.Item.parentInventory != null || placement.Item.unitCount != 1) return false;
            var slot = inventory.TryInventorySlot(placement.Item, 1, placement.Shape);
            if (slot == null || slot.Pointer == IntPtr.Zero || slot.item?.Pointer != placement.Item.Pointer ||
                slot.inventory?.Pointer != inventory.Pointer || slot.numTransfer != 1 ||
                slot.targetItem != null || !slot.IsValid() || placement.Item.parentInventory != null ||
                placement.Item.unitCount != 1 || slot.TryAcceptOnce(1) != 1 ||
                placement.Item.parentInventory?.Pointer != inventory.Pointer || placement.Item.unitCount != 1)
                return false;
        }
        return placements.All(x => x.Item.parentInventory?.Pointer == inventory.Pointer && ForgeInventoryApi.IsPlayerOwned(x.Item));
    }

    internal static bool Recall(IReadOnlyList<GameItem> products)
    {
        bool restored = true;
        foreach (var item in products)
            try
            {
                var parent = item.parentInventory;
                if (parent != null)
                {
                    bool locked = parent.overrideLockRemove; parent.overrideLockRemove = true;
                    try { if (!parent.Expel(item)) { restored = false; continue; } }
                    finally { parent.overrideLockRemove = locked; }
                }
                if (item.parentInventory != null) { restored = false; continue; }
                item.Destroy();
            }
            catch { restored = false; }
        return restored;
    }
}
