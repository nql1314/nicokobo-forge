using Nicokobo.Forge.Runtime;
using Nicokobo.Forge.Workshop;

internal static class InventoryReadChecks
{
    internal static void Run()
    {
        int checks = 0, scans = 0;
        void Expect(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception(message);
        }
        var cache = new InventoryReadCache<IReadOnlyDictionary<string, int>>();
        var key = new InventoryReadKey(new IntPtr(1), "run-a", 0, 10);
        int quantity = 1;
        IReadOnlyDictionary<string, int> Scan()
        {
            scans++;
            return new Dictionary<string, int> { ["product"] = quantity };
        }
        for (int i = 0; i < 1000; i++) Expect(cache.Capture(key, Scan)["product"] == 1, "Repeated value read lost ownership");
        Expect(scans == 1, "1000 same-frame value reads performed more than one inventory scan");
        quantity = 0;
        cache.Invalidate();
        Expect(cache.Capture(key, Scan)["product"] == 0 && scans == 2, "Selling the last product kept a same-frame effect");
        quantity = 2;
        cache.Invalidate();
        Expect(cache.Capture(key, Scan)["product"] == 2 && scans == 3, "Same-frame purchase did not activate ownership");
        Expect(cache.Capture(key with { Frame = 11 }, Scan)["product"] == 2 && scans == 4, "An unobserved native change was not rechecked next frame");
        cache.Capture(key with { Store = new IntPtr(2) }, Scan);
        cache.Capture(key with { RunId = "run-b" }, Scan);
        cache.Capture(key with { SlotId = 1 }, Scan);
        Expect(scans == 7, "A store, run or save-slot switch reused a different run's snapshot");
        cache.Invalidate();
        try { cache.Capture(key, () => throw new InvalidOperationException("unavailable")); }
        catch (InvalidOperationException) { }
        Expect(cache.Capture(key, Scan)["product"] == 2 && scans == 8, "A failed capture poisoned the next read");
        cache.Invalidate();
        cache.Capture(key, () => { cache.Invalidate(); return Scan(); });
        cache.Capture(key, Scan);
        Expect(scans == 10, "A mutation during capture published a reusable stale snapshot");

        var idle = new DragIdleGate();
        var now = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        Expect(idle.Ready(false, now), "Idle startup never refreshed achievements");
        for (int i = 0; i < 300; i++)
            Expect(!idle.Ready(true, now.AddMilliseconds(i * 16)), "Background scan ran during continuous dragging");
        var released = now.AddMilliseconds(300 * 16);
        Expect(!idle.Ready(false, released), "Release immediately triggered the expensive background scan");
        Expect(idle.Ready(false, released.AddMilliseconds(250)), "Settled drag never released the pending scan");
        Expect(!idle.Ready(true, released.AddMilliseconds(300)), "A second drag reused the old quiet interval");
        idle.Reset();
        Expect(idle.Ready(false, now), "A new run inherited the previous run's drag delay");
        Console.WriteLine($"Inventory reads and drag scheduling: {checks} assertions passed; 1000 reads -> 1 scan.");
    }
}
