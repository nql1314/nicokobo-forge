using Il2Cpp;
using Nicokobo.Forge.Runtime;
using Nicokobo.Forge.Workshop;

namespace Nicokobo.Forge;

/// <summary>A detached placement proposal. Shape is a clone, not the item's
/// current shape. Revalidate the native slot immediately before accepting.</summary>
public sealed record ForgeInventoryPlacement(GameItem Item, GridShape Shape);

/// <summary>Plans a whole batch of detached single items without changing their
/// IDs, shapes, parents or quantities, or constructing a native inventory.
/// Does not accept items, reserve live slots, grant rewards or save the game.</summary>
public static class ForgeInventoryPlacementApi
{
    private static bool _allowed;
    public static bool IsAvailable => _allowed && ForgeCapabilities.Current.KnownGameBuild &&
        ForgeCapabilities.Current.WholeTransferPreview;

    internal static void Configure(bool allowed, Action<string> log)
    {
        _allowed = false;
        if (!allowed) return;
        try { RequireContracts(); _allowed = true; }
        catch (Exception ex) { log($"[WARN] [NicokoboForge/Inventory] placement preview disabled: {ex.Message}"); }
    }

    internal static void RequireContracts()
    {
        NativeHookSet.Require(typeof(GameGridInventory), "get_inventoryShape", [], typeof(GridShape));
        NativeHookSet.Require(typeof(GameItem), "get_shape", [], typeof(GridShape));
        NativeHookSet.Require(typeof(GameItem), "get_modifiedShape", [], typeof(GridShape));
        NativeHookSet.Require(typeof(GameInventory), nameof(GameInventory.TryInventorySlot),
            [typeof(GameItem), typeof(int), typeof(GridShape), typeof(TagSystem)], typeof(SlotMarker));
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

    /// <summary>Null means unavailable, invalid items, rejected native admission
    /// or insufficient room for the whole batch. Stacking and splitting are
    /// excluded. Searches in the native row/orientation order. Returned proposals
    /// do not prove that subsequent acceptance, rollback or saving will succeed.</summary>
    public static IReadOnlyList<ForgeInventoryPlacement>? PlanWholeGrid(GameGridInventory inventory,
        IReadOnlyList<GameItem> products) => PlanGrid(inventory, products, singleUnits: true);

    // Save recovery preserves a whole saved stack and its identity. It must
    // never merge into another item or split while the persisted receipt lives.
    internal static IReadOnlyList<ForgeInventoryPlacement>? PlanWholeStacks(GameGridInventory inventory,
        IReadOnlyList<GameItem> products) => PlanGrid(inventory, products, singleUnits: false);

    private static IReadOnlyList<ForgeInventoryPlacement>? PlanGrid(GameGridInventory inventory,
        IReadOnlyList<GameItem> products, bool singleUnits)
    {
        if (!IsAvailable || inventory == null || inventory.Pointer == IntPtr.Zero || products == null ||
            products.Count > ForgeNumbers.Inventory.MaxDirectItems) return null;
        try
        {
            if (inventory.IsInsertLocked()) return null;
            var seen = new HashSet<IntPtr>();
            foreach (var product in products)
                if (product == null || product.Pointer == IntPtr.Zero || !seen.Add(product.Pointer) ||
                    product.parentInventory != null || product.unitCount <= 0 || singleUnits && product.unitCount != 1) return null;
            if (products.Count == 0) return Array.Empty<ForgeInventoryPlacement>();
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
                var child = children[i];
                if (child == null || child.Pointer == IntPtr.Zero || child.parentInventory?.Pointer != inventory.Pointer ||
                    child.modifiedShape == null) return null;
                BlockOccupied(blocked, width, height, child.modifiedShape);
            }
            var grid = new WorkshopGridPlanner(width, height, blocked);
            var result = new List<ForgeInventoryPlacement>(products.Count);
            foreach (var product in products)
            {
                var planned = ReserveShape(grid, product.shape);
                if (planned == null) return null;
                int units = product.unitCount;
                var slot = inventory.TryInventorySlot(product, units, planned);
                if (slot == null || slot.Pointer == IntPtr.Zero || slot.item?.Pointer != product.Pointer ||
                    slot.inventory?.Pointer != inventory.Pointer || slot.targetItem != null || slot.numTransfer != units ||
                    !slot.IsValid() || product.parentInventory != null || product.unitCount != units) return null;
                result.Add(new(product, planned));
            }
            return result.AsReadOnly();
        }
        catch { return null; }
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
        // Build 25382790: orientation 4,3,2,1 (4 normalizes to 0),
        // first unflipped and then flipped, for keepOrientation=false.
        for (int flip = 0; flip < 2; flip++)
            for (int turn = 0; turn < 4; turn++)
            {
                candidate.SetTransform(0, 0, flip != 0, 4 - turn);
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
}
