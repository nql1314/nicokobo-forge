using Nicokobo.Forge;

internal static class ProductionValueChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Production value: " + message); }
        Expect(ForgeProductionValueMath.PerOutput(40m, 75) == 70, "K01 markup");
        Expect(ForgeProductionValueMath.PerOutput(124m, 50) == 186, "minimum-grade ingots use actual value");
        Expect(ForgeProductionValueMath.PerOutput(200m, 50) == 300, "ungraded output includes actual input premiums");
        Expect(ForgeProductionValueMath.PerOutput(100m, 50) == 150, "graded output uses inputs without their purity premium");
        Expect(ForgeProductionValueMath.PerOutput(1250m, 25) == 1563, "full material battery value and ceiling");
        Expect(ForgeProductionValueMath.PerOutput(124.8m, 50) == 188, "fractional water cost");
        Expect(ForgeProductionValueMath.PerOutput(120m, 50) == 180, "zero water draw still prices food");
        Expect(ForgeProductionValueMath.PerOutput(108m, 50, 2) == 81, "multiple outputs divide the batch cost");
        foreach (var (value, percent, count) in new[] { (-1m, 25, 1), (1m, -1, 1), (1m, 25, 0) })
        {
            try { ForgeProductionValueMath.PerOutput(value, percent, count); throw new Exception("invalid price accepted"); }
            catch (ArgumentOutOfRangeException) { checks++; }
        }
        try { ForgeProductionValueMath.PerOutput(long.MaxValue, 25); throw new Exception("price overflow accepted"); }
        catch (OverflowException) { checks++; }
        var original = new ForgeMachineLiquidSnapshot(3_000_000,
            [new("protein", 1_000_000) { Value = 1560m, QualityBasis = 1500m }, new("water", 250_000)]);
        var after = MachineBatchMath.Consume(original, 500_000, "protein");
        Expect(after.Contents[0].Value == 780m && after.Contents[1].Parts == 250_000,
            "specific component consumption preserves other liquids");
        Expect(after.Contents[0].QualityBasis == 750m && MachineBatchMath.QualityBasis(original) -
            MachineBatchMath.QualityBasis(after) == 750m, "consumption keeps a separate pre-quality basis");
        Expect(MachineBatchMath.BaseValue(original) - MachineBatchMath.BaseValue(after) == 780m,
            "retained container and unconsumed water do not enter cost");
        var exhausted = MachineBatchMath.Consume(original, 1_000_000, "protein");
        Expect(exhausted.Contents[0].Value == null && MachineBatchMath.BaseValue(exhausted) == 10m,
            "fully consumed component has no lingering premium");
        try { MachineBatchMath.Consume(original, 1_000_001, "protein"); throw new Exception("other water replaced missing protein"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        var diluted = MachineBatchMath.Add(original, [new("protein", 1_000_000)]);
        Expect(diluted.Contents[0].Value == 1600m, "unpriced protein blends at native value");
        var sourceAfter = MachineBatchMath.Consume(original, 500_000, "protein");
        var empty = original with { Contents = [new("protein", 0), new("water", 250_000)] };
        var targetAfter = MachineBatchMath.Add(empty, [new("protein", 500_000)]);
        var transfer = MachineBatchMath.TransferValues(original, sourceAfter, empty, targetAfter);
        Expect(transfer.Source.Contents[0].Value == 780m && transfer.Target.Contents[0].Value == 780m,
            "pouring moves value with the component");
        Expect(transfer.Source.Contents[0].QualityBasis == 750m && transfer.Target.Contents[0].QualityBasis == 750m,
            "pouring preserves pre-quality cost without duplicating its premium");
        Expect(MachineBatchMath.BaseValue(transfer.Source) + MachineBatchMath.BaseValue(transfer.Target) ==
            MachineBatchMath.BaseValue(original) + MachineBatchMath.BaseValue(empty), "pouring conserves value");
        var saved = LiquidValueLedger.Encode(transfer.Target);
        var loaded = LiquidValueLedger.Decode(saved, targetAfter);
        Expect(loaded.Contents.SequenceEqual(transfer.Target.Contents), "liquid value survives serialization");
        var partialRaw = MachineBatchMath.Consume(targetAfter, 123_456, "protein");
        var partial = LiquidValueLedger.Decode(saved, partialRaw);
        Expect(partial.Contents[0].Value == 780m * 376_544 / 500_000,
            "fractional value follows a later native consumption");
        var rollback = original;
        var transaction = MachineTransaction.Run([
            new("valued-liquid", () => rollback = after, () => { rollback = original; return true; }),
            new("power-failure", () => throw new Exception("injected failure"), () => true)]);
        Expect(!transaction.Committed && transaction.Restored && rollback == original,
            "resource failure restores both volume and value");
        try { LiquidValueLedger.Decode("[{\"LiquidId\":\"protein\",\"Parts\":1,\"Value\":-1}]", empty);
            throw new Exception("invalid saved premium accepted"); }
        catch (InvalidDataException) { checks++; }
        var product = new Il2Cpp.GameItem();
        Expect(ForgeProductionValueApi.ReadBasis(product, 230) == 230,
            "new product uses its owner's default basis");
        var basisTag = new Il2Cpp.TagState(ForgeProductionValueApi.BasisTag,
            ForgeProductionValueApi.BasisTag).Enable().SetString("230");
        product.state.dict[ForgeProductionValueApi.BasisTag] = basisTag;
        product.SyncModifiedState();
        basisTag.SetString("999");
        Expect(ForgeProductionValueApi.ReadBasis(product, -1) == 230,
            "base-state mutations are invisible to native effective reads before synchronization");
        foreach (long value in new[] { 100L, 0L, long.MaxValue })
        {
            ForgeProductionValueApi.SetBasis(product, value);
            Expect(ForgeProductionValueApi.ReadBasis(product, -1) == value && product.unitValue == value,
                "new and existing production basis values reach the effective snapshot");
        }
        product.GetTagReadonly(ForgeProductionValueApi.BasisTag)!.SetString("1");
        Expect(ForgeProductionValueApi.ReadBasis(product, -1) == long.MaxValue,
            "readonly tag objects are detached clones");
        try { ForgeProductionValueApi.SetBasis(product, -1); throw new Exception("negative basis accepted"); }
        catch (ArgumentOutOfRangeException) { checks++; }
        Console.WriteLine($"Production value and liquid ledger checks passed: {checks} assertions.");
    }
}
