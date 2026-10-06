using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge.Workshop;

internal sealed record WorkshopPlacement(GameItem Item, GridShape Shape);

internal static class WorkshopRewardDelivery
{
    internal static void RequireContracts()
    {
        if (typeof(GridShapeBuilder).GetConstructor([typeof(GridShape)]) == null)
            throw new MissingMethodException(nameof(GridShapeBuilder), ".ctor(GridShape)");
        foreach (string name in new[] { "get_minX", "get_minY", "get_maxX", "get_maxY", "get_globalWidth", "get_globalHeight" })
            NativeHookSet.Require(typeof(GridShape), name, [], typeof(int));
        NativeHookSet.Require(typeof(GridShape), nameof(GridShape.GetOutsideBounds), [], typeof(byte));
        NativeHookSet.Require(typeof(GridShape), nameof(GridShape.Get), [typeof(int), typeof(int)], typeof(byte));
        NativeHookSet.Require(typeof(GridShape), nameof(GridShape.Clone), [], typeof(GridShape));
        NativeHookSet.Require(typeof(GridShapeBuilder), nameof(GridShapeBuilder.SetTransform),
            [typeof(int), typeof(int), typeof(bool), typeof(int)], typeof(GridShapeBuilder));
        NativeHookSet.Require(typeof(GridShapeBuilder), nameof(GridShapeBuilder.SetPosition),
            [typeof(int), typeof(int)], typeof(GridShapeBuilder));
        NativeHookSet.Require(typeof(GridShapeBuilder), "get_shape", [], typeof(GridShape));
        NativeHookSet.Require(typeof(GridShapeBuilder), nameof(GridShapeBuilder.Clone), [], typeof(GridShape));
    }

    // Plan the whole grant in a managed bitmap. No reward IDs, shapes or parents
    // change during preflight; native validation still excludes stacking.
    internal static List<WorkshopPlacement>? Plan(GameGridInventory inventory, IReadOnlyList<GameItem> products)
    {
        if (products.Count == 0) return [];
        if (inventory.IsInsertLocked()) return null;
        var shape = inventory.inventoryShape;
        var children = inventory.childItems;
        if (shape == null || children == null) return null;
        int width = shape.globalWidth, height = shape.globalHeight;
        if (width <= 0 || height <= 0 || width > 64 || height > 64) return null;
        var blocked = new bool[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                blocked[y * width + x] = shape.Get(x, y) != 0;
        for (int i = 0; i < children.Count; i++)
        {
            var occupied = children[i].modifiedShape;
            if (occupied != null) BlockOccupied(blocked, width, height, occupied);
        }
        var grid = new WorkshopGridPlanner(width, height, blocked);
        var result = new List<WorkshopPlacement>(products.Count);
        foreach (var product in products)
        {
            var planned = ReserveShape(grid, product.shape);
            if (planned == null) return null;
            var slot = inventory.TryInventorySlot(product, 1, planned);
            if (slot == null || slot.targetItem != null || slot.numTransfer != 1 || !slot.IsValid()) return null;
            result.Add(new(product, planned));
        }
        return result;
    }

    private static void BlockOccupied(bool[] blocked, int width, int height, GridShape shape)
    {
        int left = shape.minX, top = shape.minY, right = shape.maxX, bottom = shape.maxY;
        if (shape.GetOutsideBounds() == 1)
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (x < left || x > right || y < top || y > bottom) blocked[y * width + x] = true;
        for (int y = Math.Max(0, top); y <= Math.Min(height - 1, bottom); y++)
            for (int x = Math.Max(0, left); x <= Math.Min(width - 1, right); x++)
                if (!blocked[y * width + x] && shape.Get(x, y) == 1) blocked[y * width + x] = true;
    }

    private static GridShape? ReserveShape(WorkshopGridPlanner grid, GridShape? source)
    {
        if (source == null) return null;
        var candidate = new GridShapeBuilder(source);
        // Build 25382790 uses orientation 4,3,2,1 (4 normalizes to 0),
        // first unflipped and then flipped, for keepOrientation=false.
        for (int flip = 0; flip < 2; flip++)
            for (int turn = 0; turn < 4; turn++)
            {
                candidate.SetTransform(0, 0, flip != 0, 4 - turn);
                // Interop emits the native GridShape interface as a separate
                // wrapper, so read through the builder's declared shape view.
                var oriented = candidate.shape;
                if (oriented == null) return null;
                int width = oriented.globalWidth, height = oriented.globalHeight;
                if (width <= 0 || height <= 0 || width > 64 || height > 64) return null;
                var footprint = new List<WorkshopGridCell>();
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (oriented.Get(x, y) == 1) footprint.Add(new(x, y));
                if (!grid.TryReserve(footprint, out var position)) continue;
                candidate.SetPosition(position.X, position.Y);
                return candidate.Clone();
            }
        return null;
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
