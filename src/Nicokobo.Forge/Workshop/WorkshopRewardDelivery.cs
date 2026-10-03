using Il2Cpp;

namespace Nicokobo.Forge.Workshop;

internal sealed record WorkshopPlacement(GameItem Item, GridShape Shape);

internal static class WorkshopRewardDelivery
{
    // Plan the whole grant in a detached native grid. Existing player items are never moved.
    // Stacking is excluded so every reward keeps its own saved UID and unit count.
    internal static List<WorkshopPlacement>? Plan(GameGridInventory inventory, IReadOnlyList<GameItem> products)
    {
        if (products.Count == 0) return [];
        if (inventory.IsInsertLocked() || inventory.inventoryShape == null) return null;
        var shape = inventory.inventoryShape;
        int width = shape.globalWidth, height = shape.globalHeight;
        if (width <= 0 || height <= 0 || width > 64 || height > 64 || inventory.childItems == null) return null;
        var occupied = new List<GridShape>();
        foreach (var item in inventory.childItems)
            if (item.modifiedShape != null) occupied.Add(item.modifiedShape);
        var mask = new char[checked(width * height)];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                // Native inventory masks use 0 for an open cell; item footprints use 1.
                mask[y * width + x] = shape.Get(x, y) == 0 && !occupied.Any(item => item.Get(x, y) == 1) ? '0' : '1';
        var shadow = new GameGridInventory(new string(mask), width);
        var result = new List<WorkshopPlacement>();
        var originals = products.Select(item => item.shape.Clone()).ToArray();
        try
        {
            foreach (var product in products)
            {
                string id = product.identifier;
                SlotMarker? slot;
                try { product.identifier = id + ".forge_grant_preflight." + product.uniqueId; slot = shadow.TryFindOneValidInventorySlot(product, false); }
                finally { product.identifier = id; }
                if (slot == null || slot.targetItem != null || !slot.IsValid()) return null;
                var planned = slot.itemGridShape.Clone();
                if (slot.TryAcceptOnce(1) != 1 || product.parentInventory?.Pointer != shadow.Pointer) return null;
                result.Add(new(product, planned));
            }
        }
        finally
        {
            for (int i = 0; i < products.Count; i++)
            {
                var item = products[i];
                if (item.parentInventory?.Pointer == shadow.Pointer && !shadow.Expel(item))
                    throw new InvalidOperationException("Reward preflight detach failed");
                item.SetShape(originals[i]);
            }
            GC.KeepAlive(shadow); // No explicit native destructor; the detached grid is GC owned.
        }
        foreach (var placement in result)
        {
            var slot = inventory.TryInventorySlot(placement.Item, 1, placement.Shape);
            if (slot == null || slot.targetItem != null || slot.numTransfer != 1 || !slot.IsValid()) return null;
        }
        return result;
    }

    internal static bool Apply(GameGridInventory inventory, IReadOnlyList<WorkshopPlacement> placements)
    {
        foreach (var placement in placements)
        {
            var slot = inventory.TryInventorySlot(placement.Item, 1, placement.Shape);
            if (slot == null || slot.targetItem != null || !slot.IsValid() || slot.TryAcceptOnce(1) != 1 ||
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
