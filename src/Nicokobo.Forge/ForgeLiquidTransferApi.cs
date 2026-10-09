using Il2Cpp;

namespace Nicokobo.Forge;

public sealed record ForgeLiquidTransferResult(int PartsMoved, bool Indeterminate, string Status);

/// <summary>Pours through the native transfer path, retaining both vessels.
/// Content owns liquid/quality selection; this adapter verifies both sides.</summary>
public static class ForgeLiquidTransferApi
{
    /// <summary>Transfers at most the supplied amount from the whole native
    /// mixture. A bounded pour preserves every component and value ledger; it
    /// never selects one component. Each side is compensated on failure.</summary>
    public static ForgeLiquidTransferResult Pour(GameItem source, GameItem target, int maximumParts,
        Func<bool>? stillAllowed = null)
    {
        if (maximumParts <= 0 || stillAllowed?.Invoke() == false) return new(0, false, "reserve-or-condition");
        var from = ForgeLiquidApi.Capture(source); var into = ForgeLiquidApi.Capture(target);
        if (from == null || into == null) return new(0, false, "unavailable");
        int natural = Math.Min(from.TotalParts, into.CapacityParts - into.TotalParts);
        if (maximumParts >= natural) return Pour(source, target);
        if (!ForgeCapabilities.Current.KnownGameBuild || !ForgeMachineRuntimeApi.RuntimeInstalled ||
            source.Pointer == target.Pointer || !ForgeInventoryApi.IsPlayerOwned(source) || !ForgeInventoryApi.IsPlayerOwned(target) ||
            source.parentInventory is not { } sourceParent || target.parentInventory is not { } targetParent ||
            sourceParent.IsRemoveLocked() || targetParent.IsInsertLocked() || source.unitCount != 1 || target.unitCount != 1 ||
            WaterHelper.GetFilterItem(source) != null || WaterHelper.GetFilterItem(target) != null)
            return new(0, false, "locked-or-filtered");
        using var frozen = ForgeInventoryFreezeApi.TryAcquire([source, target]);
        if (frozen == null) return new(0, false, "busy");
        var sourceState = source.state.Clone(); var targetState = target.state.Clone();
        long sourceValue = source.unitValue, targetValue = target.unitValue;
        var afterSource = MachineBatchMath.Consume(from, maximumParts, null);
        var drawn = ForgeLiquidCompositionMath.Consumed(from, afterSource);
        var afterTarget = MachineBatchMath.Add(into, drawn);
        bool Current() => source.parentInventory?.Pointer == sourceParent.Pointer && target.parentInventory?.Pointer == targetParent.Pointer &&
            source.unitCount == 1 && target.unitCount == 1 && ForgeInventoryApi.IsPlayerOwned(source) && ForgeInventoryApi.IsPlayerOwned(target);
        bool Restore(GameItem item, TagSystem state, long value, ForgeMachineLiquidSnapshot snapshot)
        {
            if (!Current()) return false;
            item.state = state.Clone(); item.unitValue = value; item.SyncModifiedState(); SpriteHelper.UpdateWaterContainerSprite(item);
            return ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(item), snapshot);
        }
        var result = ForgeTransactionApi.Run([
            new("bounded-pour-source", () =>
            {
                if (!Current() || stillAllowed?.Invoke() == false || !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(source), from) ||
                    !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(target), into) || !ForgeLiquidApi.Write(source, afterSource))
                    throw new InvalidOperationException("source changed or bounded draw failed");
            }, () => Restore(source, sourceState, sourceValue, from)),
            new("bounded-pour-target", () =>
            {
                if (!Current() || !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(target), into) || !ForgeLiquidApi.Write(target, afterTarget) ||
                    !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(source), afterSource))
                    throw new InvalidOperationException("target changed or bounded receive failed");
                SpriteHelper.UpdateWaterContainerSprite(source); SpriteHelper.UpdateWaterContainerSprite(target);
            }, () => Restore(target, targetState, targetValue, into))]);
        return new(result.Committed ? maximumParts : 0, !result.Committed && !result.Restored, result.Status);
    }
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
