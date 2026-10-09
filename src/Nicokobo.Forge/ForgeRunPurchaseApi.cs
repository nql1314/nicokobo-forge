using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Actual native cash cost and caller-owned state commit together.
/// The caller chooses costs and prerequisites; this adapter only compensates
/// the current run's observed cash and the supplied transaction steps.</summary>
public static class ForgeRunPurchaseApi
{
    public static ForgeTransactionResult Purchase(PlayerStore store, int cost, IReadOnlyList<ForgeTransactionStep> steps)
    {
        if (store == null || cost <= 0 || steps == null || steps.Count == 0 || !store.CanPay(cost))
            return new(false, true, "purchase-unavailable");
        string run = store.runID; int slot = store.saveSlotId, cash = store.GetCash();
        bool Current() => PlayerStore.instance?.Pointer == store.Pointer && store.runID == run && store.saveSlotId == slot;
        var cashStep = new ForgeTransactionStep("native-cash", () =>
        {
            if (!Current() || store.GetCash() != cash || !store.CanPay(cost)) throw new InvalidOperationException("cash changed");
            store.PayMoney(cost, false);
            if (!Current() || store.GetCash() != cash - cost) throw new InvalidOperationException("cash readback mismatch");
        }, () =>
        {
            if (!Current() || store.GetCash() != cash && store.GetCash() != cash - cost) return false;
            store.playerCash = cash; return Current() && store.GetCash() == cash;
        });
        return ForgeTransactionApi.Run(new[] { cashStep }.Concat(steps).ToArray());
    }
}
