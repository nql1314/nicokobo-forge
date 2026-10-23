using Il2Cpp;
using Nicokobo.Forge;

internal static class LiquidWriteChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool condition, string message)
        { checks++; if (!condition) throw new Exception("Liquid write: " + message); }
        GameItem Vessel(int total, int purity, bool identified)
        {
            int water = (int)((long)total * purity / 10000);
            var item = new GameItem { Liquid = new(41_750_000,
                [new("water", water), new("mineral", total - water)]) };
            WaterFeatureHelper.UpdateWaterFeature(item, identified);
            return item;
        }

        // Exercise the production writer against the native empty/add feature
        // transitions, not a writer stub which replaces the snapshot directly.
        foreach (int purity in new[] { 10000, 9990, 9900, 9800, 9500, 8500 })
        foreach (int millilitres in new[] { 2344, 10000, 41750 })
        {
            var item = Vessel(millilitres * 1000, purity, true);
            var after = MachineBatchMath.Consume(item.Liquid!, 500_000, null);
            Expect(ForgeLiquidApi.Write(item, after), "proportional debit must pass component readback");
            Expect(item.WaterFeature.IsExposed() && item.WaterFeature.DisplayedPurity == purity,
                $"identified water {purity} at {millilitres} ml lost its displayed quality after a batch");
            Expect(item.Liquid!.TotalParts == (millilitres - 500) * 1000,
                "preserving identification must not restore consumed water");
        }
        {
            var item = Vessel(2_344_000, 9900, false);
            Expect(ForgeLiquidApi.Write(item, MachineBatchMath.Consume(item.Liquid!, 500_000, null)), "unknown water debit");
            Expect(!item.WaterFeature.IsExposed(), "writing unknown water must not reveal its quality");
        }
        {
            var item = Vessel(2_000_000, 9900, true);
            var changed = item.Liquid! with { Contents = [new("water", 1_000_000), new("mineral", 1_000_000)] };
            Expect(ForgeLiquidApi.Write(item, changed), "changed mixture write");
            Expect(!item.WaterFeature.IsExposed() && item.WaterFeature.RealPurity == 5000,
                "a real purity change must invalidate the previous identification");
        }
        {
            var item = Vessel(500_000, 10000, true);
            Expect(ForgeLiquidApi.Write(item, MachineBatchMath.Consume(item.Liquid!, 500_000, null)), "empty write");
            Expect(item.Liquid!.TotalParts == 0 && !item.WaterFeature.IsExposed(),
                "an emptied container must remain empty and unidentified");
        }
        {
            var item = Vessel(2_000_000, 9900, true);
            item.Liquid = item.Liquid! with { Contents = item.Liquid.Contents.Select(part => part with
                { Value = part.Parts / 1000m, QualityBasis = part.Parts / 2000m }).ToArray() };
            var after = MachineBatchMath.Consume(item.Liquid, 250_000, null);
            Expect(ForgeLiquidApi.Write(item, after) && item.Liquid!.Contents.SequenceEqual(after.Contents),
                "identification refresh must preserve each component's value and quality basis");
        }
        Console.WriteLine($"Liquid writes: {checks} offline checks passed (native feature transitions, purity, volume and values).");
    }
}
