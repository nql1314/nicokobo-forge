using Nicokobo.Forge.Runtime;
using Nicokobo.Forge.Workshop;
using Nicokobo.Forge;

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
        var cache = new InventoryReadCache<IReadOnlyDictionary<string, int>>(ForgeNumbers.Inventory.OwnedCountCacheSeconds);
        var key = new InventoryReadKey(new IntPtr(1), "run-a", 0);
        int quantity = 1;
        IReadOnlyDictionary<string, int> Scan()
        {
            scans++;
            return new Dictionary<string, int> { ["product"] = quantity };
        }
        for (int i = 0; i < 1000; i++) Expect(cache.Capture(key, 0, Scan)["product"] == 1, "Repeated value read lost ownership");
        Expect(scans == 1, "1000 same-frame value reads performed more than one inventory scan");
        quantity = 0;
        cache.Invalidate();
        Expect(cache.Capture(key, 0, Scan)["product"] == 0 && scans == 2, "Selling the last product kept a same-frame effect");
        quantity = 2;
        cache.Invalidate();
        Expect(cache.Capture(key, 0, Scan)["product"] == 2 && scans == 3, "Same-frame purchase did not activate ownership");
        quantity = 4; // Direct field write with no mutation hook.
        Expect(cache.Capture(key, 0.249, Scan)["product"] == 2 && scans == 3,
            "Unchanged inventory was rescanned on a later frame before expiry");
        Expect(cache.Capture(key, 0.25, Scan)["product"] == 4 && scans == 4,
            "An unobserved direct write outlived the 250 ms snapshot");
        cache.Capture(key with { Store = new IntPtr(2) }, 0.25, Scan);
        cache.Capture(key with { RunId = "run-b" }, 0.25, Scan);
        cache.Capture(key with { SlotId = 1 }, 0.25, Scan);
        Expect(scans == 7, "A store, run or save-slot switch reused a different run's snapshot");
        cache.Invalidate();
        try { cache.Capture(key, 0.25, () => throw new InvalidOperationException("unavailable")); }
        catch (InvalidOperationException) { }
        Expect(cache.Capture(key, 0.25, Scan)["product"] == 4 && scans == 8, "A failed capture poisoned the next read");
        cache.Invalidate();
        cache.Capture(key, 0.25, () => { cache.Invalidate(); return Scan(); });
        cache.Capture(key, 0.25, Scan);
        Expect(scans == 10, "A mutation during capture published a reusable stale snapshot");

        quantity = 5;
        Expect(cache.Capture(key, 0.1, Scan)["product"] == 5 && scans == 11,
            "A backwards clock reused a future snapshot");
        try { cache.Capture(key, 0.4, () => throw new InvalidOperationException("unavailable")); }
        catch (InvalidOperationException) { }
        quantity = 6;
        Expect(cache.Capture(key, 0.1, Scan)["product"] == 6 && scans == 12,
            "A failed expired read revived the prior snapshot after a clock reset");
        cache.Invalidate();
        int beforeContinuousReads = scans;
        for (int frame = 0; frame < 3600; frame++)
            for (int item = 0; item < 100; item++)
                cache.Capture(key, frame / 60d, Scan);
        int continuousScans = scans - beforeContinuousReads;
        Expect(continuousScans <= 240, "One minute of unchanged shop price reads still scanned inventory every frame");

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
        Console.WriteLine($"Inventory reads and drag scheduling: {checks} assertions passed; 360000 reads over 3600 frames -> {continuousScans} scans.");
    }
}
