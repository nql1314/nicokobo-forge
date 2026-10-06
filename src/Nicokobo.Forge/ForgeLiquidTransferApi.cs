using Il2Cpp;

namespace Nicokobo.Forge;

public sealed record ForgeLiquidTransferResult(int PartsMoved, bool Indeterminate, string Status);

/// <summary>Pours through the native transfer path, retaining both vessels.
/// Content owns liquid/quality selection; this adapter verifies both sides.</summary>
public static class ForgeLiquidTransferApi
{
    public static ForgeLiquidTransferResult Pour(GameItem source, GameItem target)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || !ForgeMachineRuntimeApi.RuntimeInstalled ||
            source == null || target == null || source.Pointer == target.Pointer ||
            !ForgeInventoryApi.IsPlayerOwned(source) || !ForgeInventoryApi.IsPlayerOwned(target)) return new(0, false, "unavailable");
        var sourceParent = source.parentInventory; var targetParent = target.parentInventory;
        if (sourceParent == null || targetParent == null || sourceParent.IsRemoveLocked() || targetParent.IsInsertLocked())
            return new(0, false, "locked");
        var beforeSource = ForgeLiquidApi.Capture(source); var beforeTarget = ForgeLiquidApi.Capture(target);
        if (beforeSource == null || beforeTarget == null || beforeSource.TotalParts == 0 ||
            beforeTarget.TotalParts >= beforeTarget.CapacityParts) return new(0, false, "empty-or-full");
        string? failure = null;
        try { WaterHelper.TransferLiquid(source, target); }
        catch (Exception ex) { failure = ex.GetType().Name; }
        // A native callback can throw after the pour has committed. Observe both
        // vessels even then; confirmed transfers must not be replayed or refunded.
        try
        {
            var afterSource = ForgeLiquidApi.Capture(source); var afterTarget = ForgeLiquidApi.Capture(target);
            if (afterSource == null || afterTarget == null || source.parentInventory?.Pointer != sourceParent.Pointer ||
                target.parentInventory?.Pointer != targetParent.Pointer || source.unitCount != 1 || target.unitCount != 1)
                return new(0, true, "vessel-readback-mismatch");
            int moved = beforeSource.TotalParts - afterSource.TotalParts;
            bool valid = moved >= 0 && moved == afterTarget.TotalParts - beforeTarget.TotalParts &&
                moved <= Math.Min(beforeSource.TotalParts, beforeTarget.CapacityParts - beforeTarget.TotalParts) &&
                afterSource.CapacityParts == beforeSource.CapacityParts && afterTarget.CapacityParts == beforeTarget.CapacityParts;
            foreach (var id in beforeSource.Contents.Concat(beforeTarget.Contents)
                .Concat(afterSource.Contents).Concat(afterTarget.Contents).Select(c => c.LiquidId).Distinct())
            {
                long Amount(ForgeMachineLiquidSnapshot s) => s.Contents.Where(p => p.LiquidId == id).Sum(p => (long)p.Parts);
                long removed = Amount(beforeSource) - Amount(afterSource);
                valid &= removed >= 0 && removed == Amount(afterTarget) - Amount(beforeTarget);
            }
            decimal Value(ForgeMachineLiquidSnapshot s) => ForgeLiquidCompositionMath.BaseValue(s.Contents);
            valid &= Value(beforeSource) + Value(beforeTarget) == Value(afterSource) + Value(afterTarget);
            decimal Basis(ForgeMachineLiquidSnapshot s) => s.Contents.Sum(p => p.QualityBasis ?? p.Value ?? ForgeProductionValueMath.NativeLiquidValue(p.Parts));
            valid &= Basis(beforeSource) + Basis(beforeTarget) == Basis(afterSource) + Basis(afterTarget);
            if (moved == 0)
                valid &= beforeSource.Contents.SequenceEqual(afterSource.Contents) &&
                    beforeTarget.Contents.SequenceEqual(afterTarget.Contents);
            else if (valid)
            {
                // A conserved combined total alone can hide metadata left in
                // the source or assigned to another component. Match the same
                // proportional ledger rule used by the native pour postfix.
                var expected = MachineBatchMath.TransferValues(beforeSource, afterSource, beforeTarget, afterTarget);
                valid &= expected.Source.Contents.SequenceEqual(afterSource.Contents) &&
                    expected.Target.Contents.SequenceEqual(afterTarget.Contents);
            }
            return valid ? new(moved, false, moved == 0 ? "rejected" : failure == null ? "poured" : "poured-readback")
                : new(0, true, failure ?? "liquid-readback-mismatch");
        }
        catch (Exception ex) { return new(0, true, ex.GetType().Name); }
    }
}
