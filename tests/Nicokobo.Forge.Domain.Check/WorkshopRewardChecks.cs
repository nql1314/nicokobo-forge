using Nicokobo.Forge.Workshop;

internal static class WorkshopRewardChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string reason)
        {
            checks++;
            if (!value) throw new Exception(reason);
        }
        var single = new[] { new WorkshopGridCell(0, 0) };
        var card = new[] { new WorkshopGridCell(0, 0), new WorkshopGridCell(1, 0) };

        bool[] input = [true, false, false, false, false, false];
        var grid = new WorkshopGridPlanner(3, 2, input);
        Expect(grid.TryReserve(card, out var cardPos) && cardPos == new WorkshopGridCell(1, 0),
            "Card did not use the first free pair in native row order");
        Expect(grid.TryReserve(single, out var next) && next == new WorkshopGridCell(0, 1),
            "The next reward overlapped the planned card");
        Expect(input.SequenceEqual(new[] { true, false, false, false, false, false }),
            "Preflight modified the original occupancy snapshot");

        var fragmented = new WorkshopGridPlanner(3, 1, [false, true, false]);
        Expect(!fragmented.TryReserve(card, out _), "Fragmented free area accepted a two-cell card");
        Expect(fragmented.TryReserve(single, out next) && next == new WorkshopGridCell(0, 0),
            "A failed candidate reserved cells");
        Expect(fragmented.TryReserve(single, out next) && next == new WorkshopGridCell(2, 0),
            "Existing blocked cells were overwritten");
        Expect(!fragmented.TryReserve(single, out _), "A full bitmap accepted another reward");

        var narrow = new WorkshopGridPlanner(1, 2, [false, false]);
        Expect(!narrow.TryReserve(card, out _), "Unrotated card fit a one-cell-wide inventory");
        Expect(narrow.TryReserve([new(0, 0), new(0, 1)], out next) && next == new WorkshopGridCell(0, 0),
            "Rotated card could not use the available column");

        var hole = new WorkshopGridPlanner(2, 2, [false, false, false, true]);
        Expect(hole.TryReserve([new(0, 0), new(1, 0), new(0, 1)], out _),
            "A shape's empty corner collided with an existing item");
        Expect(!hole.TryReserve(single, out _), "Irregular reward cells were not all reserved");

        var mirror = new WorkshopGridPlanner(2, 2, [true, false, false, false]);
        Expect(!mirror.TryReserve([new(0, 0), new(1, 0), new(0, 1)], out _),
            "Unflipped footprint fit the blocked corner");
        Expect(mirror.TryReserve([new(1, 0), new(0, 1), new(1, 1)], out _),
            "Mirrored footprint lost the compatible empty corner");

        var emptyBorder = new WorkshopGridPlanner(2, 1, [true, false]);
        Expect(emptyBorder.TryReserve(single, out next) && next == new WorkshopGridCell(1, 0),
            "Empty shape borders outside the grid prevented native-compatible placement");
        var outside = new WorkshopGridPlanner(2, 1, [false, false]);
        Expect(!outside.TryReserve([new(2, 0)], out _), "Occupied footprint cell escaped inventory bounds");
        Expect(outside.TryReserve(card, out _), "Rejected out-of-bounds shape consumed room");

        var batch = new WorkshopGridPlanner(2, 1, [false, false]);
        Expect(batch.TryReserve(card, out _) && !batch.TryReserve(single, out _),
            "Partial batch capacity was reported as sufficient for every reward");

        foreach (var invalid in new[] { (0, 1, 0), (65, 1, 65), (2, 2, 3) })
        {
            bool rejected = false;
            try { _ = new WorkshopGridPlanner(invalid.Item1, invalid.Item2, new bool[invalid.Item3]); }
            catch (ArgumentException) { rejected = true; }
            Expect(rejected, "Invalid grid dimensions or data length were accepted");
        }
        Console.WriteLine($"Workshop reward placement checks passed: {checks}");
    }
}
