using Il2Cpp;
using Nicokobo.Forge;

internal static class LiquidTransferChecks
{
    internal static void Run()
    {
        int checks = 0;
        var failures = new List<string>();
        void Expect(bool result, string message)
        { checks++; if (!result) failures.Add(message); }
        var savedCapabilities = ForgeCapabilities.Current;
        ForgeCapabilities.Publish(savedCapabilities with { KnownGameBuild = true });
        (GameItem Source, GameItem Target) Setup()
        {
            WaterHelper.Transfer = null;
            var source = new GameItem { Pointer = new(701), Owned = true, parentInventory = new GameGridInventory { Pointer = new(703) },
                Liquid = new(100, [new("water", 60) { Value = 30, QualityBasis = 20 }, new("wine", 20) { Value = 10, QualityBasis = 5 }]) };
            var target = new GameItem { Pointer = new(702), Owned = true, parentInventory = new GameGridInventory { Pointer = new(704) },
                Liquid = new(100, [new("water", 0), new("wine", 0)]) };
            return (source, target);
        }
        void PourHalf(GameItem source, GameItem target)
        {
            source.Liquid = new(100, [new("water", 30) { Value = 15, QualityBasis = 10 }, new("wine", 10) { Value = 5, QualityBasis = 2.5m }]);
            target.Liquid = new(100, [new("water", 30) { Value = 15, QualityBasis = 10 }, new("wine", 10) { Value = 5, QualityBasis = 2.5m }]);
        }
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = PourHalf;
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 40 && !result.Indeterminate, "Conserved pour did not report actual volume");
        }
        {
            var (source, target) = Setup(); int nativeCalls = WaterHelper.TransferCalls;
            var result = ForgeLiquidTransferApi.Pour(source, target, 20);
            Expect(result.PartsMoved == 20 && !result.Indeterminate && source.Liquid!.TotalParts == 60 && target.Liquid!.TotalParts == 20,
                "A reserve-bounded pour must move exactly the allowed whole-mixture amount");
            Expect(target.Liquid!.Contents.Single(part => part.LiquidId == "water").Parts == 15 &&
                target.Liquid.Contents.Single(part => part.LiquidId == "wine").Parts == 5 &&
                target.Liquid.Contents.Sum(part => part.Value ?? 0) == 10 && target.Liquid.Contents.Sum(part => part.QualityBasis ?? 0) == 6.25m,
                "A bounded pour must carry every component and its proportional value/basis");
            Expect(WaterHelper.TransferCalls == nativeCalls, "A bounded adapter must not call an unbounded native transfer then fabricate a refund");
        }
        {
            var (source, target) = Setup(); int calls = WaterHelper.TransferCalls;
            var result = ForgeLiquidTransferApi.Pour(source, target, 20, () => false);
            Expect(result.PartsMoved == 0 && !result.Indeterminate && source.Liquid!.TotalParts == 80 && target.Liquid!.TotalParts == 0 && WaterHelper.TransferCalls == calls,
                "A changed reservation must reject a bounded pour before writing either vessel");
        }
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = (from, to) => { PourHalf(from, to); throw new InvalidOperationException("Native callback failed after transfer"); };
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 40 && !result.Indeterminate && result.Status == "poured-readback",
                "Confirmed committed pour was hidden by a later native callback exception");
        }
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = (_, _) => throw new InvalidOperationException("Native rejected before writing");
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 0 && !result.Indeterminate && result.Status == "rejected",
                "Unchanged rejected pour was reported as an uncertain write");
        }
        foreach (Action<GameItem, GameItem> corrupt in new Action<GameItem, GameItem>[]
        {
            (from, to) => { PourHalf(from, to); to.Liquid = to.Liquid! with { CapacityParts = 101 }; },
            (from, to) => { PourHalf(from, to); to.Liquid = new(100, [new("water", 29) { Value = 15, QualityBasis = 10 }, new("wine", 11) { Value = 5, QualityBasis = 2.5m }]); },
            (from, to) => { PourHalf(from, to); to.Liquid = new(100, [new("water", 30) { Value = 16, QualityBasis = 10 }, new("wine", 10) { Value = 5, QualityBasis = 2.5m }]); },
            (from, to) => { PourHalf(from, to); to.Liquid = new(100, [new("water", 30) { Value = 15, QualityBasis = 11 }, new("wine", 10) { Value = 5, QualityBasis = 2.5m }]); },
            (from, to) => { PourHalf(from, to); to.parentInventory = new GameGridInventory { Pointer = new(705) }; },
            (from, to) => { PourHalf(from, to); to.unitCount = 2; },
            (from, to) => { PourHalf(from, to); from.ThrowLiquidRead = true; }
        })
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = corrupt;
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 0 && result.Indeterminate,
                "Component/value/basis/capacity/vessel mismatch was reported committed");
        }
        {
            var (source, target) = Setup();
            source.parentInventory!.overrideLockRemove = true;
            int before = WaterHelper.TransferCalls;
            Expect(ForgeLiquidTransferApi.Pour(source, target).Status == "locked" && WaterHelper.TransferCalls == before,
                "Locked pour called the native mutation");
        }
        {
            var (source, target) = Setup();
            target.Liquid = new(100, [new("water", 0), new("wine", 30) { Value = 15, QualityBasis = 7.5m }]);
            WaterHelper.Transfer = (from, to) =>
            {
                // Net volume and total component/value/basis balances conserve,
                // but wine flowed backwards during a one-way pour.
                from.Liquid = new(100, [new("water", 30) { Value = 15, QualityBasis = 10 }, new("wine", 30) { Value = 15, QualityBasis = 7.5m }]);
                to.Liquid = new(100, [new("water", 30) { Value = 15, QualityBasis = 10 }, new("wine", 20) { Value = 10, QualityBasis = 5 }]);
            };
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 0 && result.Indeterminate,
                "A one-way pour accepted an unexpected reverse component exchange");
        }
        foreach (bool corruptBasis in new[] { false, true })
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = (from, to) =>
            {
                PourHalf(from, to);
                // Both total balances still conserve. Metadata for the moved
                // component must travel proportionally with its actual volume.
                from.Liquid = from.Liquid! with { Contents = from.Liquid.Contents.Select(part =>
                    part.LiquidId != "water" ? part : corruptBasis ? part with { QualityBasis = 20 } : part with { Value = 30 }).ToArray() };
                to.Liquid = to.Liquid! with { Contents = to.Liquid.Contents.Select(part =>
                    part.LiquidId != "water" ? part : corruptBasis ? part with { QualityBasis = 0 } : part with { Value = 0 }).ToArray() };
            };
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 0 && result.Indeterminate,
                "A conserved total hid value or quality basis left behind in the source");
        }
        {
            var (source, target) = Setup();
            WaterHelper.Transfer = (from, to) =>
            {
                PourHalf(from, to);
                // Even each vessel's total value is unchanged, but another
                // component cannot inherit water's own premium.
                to.Liquid = new(100, [new("water", 30) { Value = 14, QualityBasis = 9 },
                    new("wine", 10) { Value = 6, QualityBasis = 3.5m }]);
            };
            var result = ForgeLiquidTransferApi.Pour(source, target);
            Expect(result.PartsMoved == 0 && result.Indeterminate,
                "A conserved vessel total hid metadata shifted to another component");
        }
        ForgeCapabilities.Publish(savedCapabilities);
        WaterHelper.Transfer = null;
        if (failures.Count > 0) throw new Exception("Liquid transfer: " + string.Join("; ", failures));
        Console.WriteLine($"Liquid transfers: {checks} offline checks passed (committed exceptions, rejection, component/value/basis conservation).");
    }
}
