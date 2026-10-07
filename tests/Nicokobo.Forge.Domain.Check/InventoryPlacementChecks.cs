using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class InventoryPlacementChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool result, string message)
        { checks++; if (!result) throw new Exception("Placement preview: " + message); }
        var savedCapabilities = ForgeCapabilities.Current;
        ForgeCapabilities.Publish(savedCapabilities with { KnownGameBuild = true, WholeTransferPreview = true });
        NativeHookSet.ContractsMatch = true;
        ForgeInventoryPlacementApi.Configure(true, _ => { });
        GameGridInventory Grid(int width, int height, params byte[] mask) => new()
            { Pointer = new(900), PlacementFixture = true, inventoryShape = new(width, height, mask) };
        GameItem Product(int pointer, int width = 1, int height = 1, params byte[] cells) => new()
            { Pointer = new(pointer), identifier = "reward", shape = new(width, height, cells.Length == 0 ? [1] : cells) };

        var grid = Grid(3, 2, 0, 0, 0, 0, 0, 0);
        var occupied = Product(901);
        occupied.parentInventory = grid;
        occupied.modifiedShape = new(1, 1, 1) { X = 0, Y = 0 };
        grid.childItems.Add(occupied);
        var card = Product(902, 2, 1, 1, 1);
        var single = Product(903);
        var originalShape = card.modifiedShape;
        var plan = ForgeInventoryPlacementApi.PlanWholeGrid(grid, [card, single]);
        Expect(plan is { Count: 2 } && plan[0].Shape.minX == 1 && plan[0].Shape.minY == 0 &&
            plan[1].Shape.minX == 0 && plan[1].Shape.minY == 1, "Batch did not reserve shapes in native row order");
        Expect(card.identifier == "reward" && card.parentInventory == null && card.unitCount == 1 &&
            ReferenceEquals(card.modifiedShape, originalShape) && grid.childItems.Count == 1 && grid.PreviewCalls == 2,
            "Planning wrote an item, constructed shadow contents, or skipped native slot checks");
        plan![0].Shape.Cells[0] = 0;
        Expect(card.shape.Get(0, 0) == 1 && card.modifiedShape == originalShape,
            "Caller could mutate an original item through the proposal's cloned shape");

        var narrow = Grid(1, 2, 0, 0);
        var rotated = ForgeInventoryPlacementApi.PlanWholeGrid(narrow, [card]);
        Expect(rotated is { Count: 1 } && rotated[0].Shape.globalWidth == 1 && rotated[0].Shape.globalHeight == 2,
            "A rotatable card did not fit a narrow inventory");
        var full = Grid(2, 1, 0, 0);
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(full, [card, single]) == null &&
            card.parentInventory == null && single.parentInventory == null && full.childItems.Count == 0,
            "Insufficient whole-batch capacity partially inserted rewards");
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(Grid(3, 1, 0, 1, 0), [card]) == null,
            "Fragmented mask was ignored");
        var irregular = Product(904, 2, 2, 1, 1, 1, 0);
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(Grid(2, 2, 0, 0, 0, 1), [irregular]) is { Count: 1 },
            "An irregular shape's empty corner occupied a blocked cell");
        var boundary = Grid(2, 1, 1, 0);
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(boundary, [Product(905, 2, 1, 1, 0)]) is { Count: 1 } border &&
            border[0].Shape.minX == 1, "An empty shape border could not extend beyond the inventory");
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(Grid(1, 1, 0), [Product(906, 1, 1, 0)]) == null,
            "Empty item geometry was treated as a placed reward");

        grid.RejectPlacementPreview = true;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null, "Native rejection was ignored");
        grid.RejectPlacementPreview = false;
        grid.MergePlacementPreview = true;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null, "Native stacking was allowed");
        grid.MergePlacementPreview = false;
        single.unitCount = 2;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null, "Stacked reward was admitted");
        Expect(ForgeInventoryPlacementApi.PlanWholeStacks(grid, [single]) is { Count: 1 } &&
            grid.LastPlacementSlot?.numTransfer == 2 && single.unitCount == 2 && single.parentInventory == null,
            "Recovery stack was split or its full native acceptance was not previewed");
        grid.MergePlacementPreview = true;
        Expect(ForgeInventoryPlacementApi.PlanWholeStacks(grid, [single]) == null, "Recovery merged a saved identity");
        grid.MergePlacementPreview = false;
        single.unitCount = 1;
        single.parentInventory = grid;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null, "An attached reward was admitted");
        single.parentInventory = null;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single, single]) == null, "A repeated item was planned twice");
        grid.overrideLockInsert = true;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null, "Insert lock was bypassed");
        grid.overrideLockInsert = false;
        grid.DuringPlacementPreview = () => single.parentInventory = grid;
        Expect(ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null,
            "Native admission changing a product's parent went undetected");
        single.parentInventory = null;
        grid.DuringPlacementPreview = null;

        ForgeInventoryPlacementApi.Configure(false, _ => { });
        Expect(!ForgeInventoryPlacementApi.IsAvailable && ForgeInventoryPlacementApi.PlanWholeGrid(grid, [single]) == null,
            "Disabled preview attempted native placement");
        NativeHookSet.ContractsMatch = false;
        ForgeInventoryPlacementApi.Configure(true, _ => { });
        Expect(!ForgeInventoryPlacementApi.IsAvailable, "Missing geometry signature enabled preview");
        NativeHookSet.ContractsMatch = true;
        ForgeInventoryPlacementApi.Configure(true, _ => { });
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { KnownGameBuild = false });
        Expect(!ForgeInventoryPlacementApi.IsAvailable, "Unknown build enabled preview");
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { KnownGameBuild = true, WholeTransferPreview = false });
        Expect(!ForgeInventoryPlacementApi.IsAvailable, "Missing native inventory gate enabled preview");
        ForgeCapabilities.Publish(savedCapabilities);
        Console.WriteLine($"Public inventory placement: {checks} offline checks passed; temporary inventories=0; item writes=0.");
    }
}
