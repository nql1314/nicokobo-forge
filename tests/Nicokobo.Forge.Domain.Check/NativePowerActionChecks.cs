using Nicokobo.Forge;

internal static class NativePowerActionChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }

        var idle = new NativeOperation();
        var action = new NativePowerAction(idle.Plan);
        Check(action.Verify() && !action.HasReservation && !action.WasDrawn,
            "An idle native callback needs no grid receipt or matching Draw");
        Check(idle.Complete(action) && idle.PlanCalls == 0 && idle.Grid == 100 && idle.Local == 17 &&
            idle.Material == 6 && idle.Output == 0 && idle.RestoreCalls == 0,
            "An idle callback must leave local battery, grid, material and output unchanged");

        var local = new NativeOperation();
        action = new NativePowerAction(local.Plan);
        local.Local -= 7;
        local.Work();
        Check(local.Complete(action) && local.Local == 10 && local.Grid == 100 && local.PlanCalls == 0,
            "A native local-battery operation without external borrowing commits without a grid debit");

        var unused = new NativeOperation();
        action = new NativePowerAction(unused.Plan);
        Check(action.Prepare(30) && unused.Grid == 70 && unused.Local == 17 && action.HasReservation,
            "CanDraw must reserve actual grid energy while leaving the physical local battery untouched");
        Check(action.Prepare(30) && action.Prepare(30) && unused.PlanCalls == 1 && unused.ValidateCalls == 1 &&
            unused.DebitCalls == 1 && unused.Grid == 70,
            "Repeated CanDraw for one native action must reuse one frozen receipt rather than debit again");
        Check(!action.Verify() && !action.Failed && !action.WasDrawn,
            "CanDraw without a corresponding Draw is an uncommitted reservation");
        Check(!unused.Complete(action) && unused.Refunded && unused.RestoreCalls == 1 && unused.DisposeCalls == 1,
            "A native callback that returns after CanDraw must return its borrowed energy and release the receipt");

        var committed = new NativeOperation();
        action = new NativePowerAction(committed.Plan);
        Check(action.Prepare(30) && action.Draw(30) && action.Verify() && action.WasDrawn,
            "Matching CanDraw and Draw must permit the native operation to commit");
        committed.Work();
        Check(committed.Complete(action) && committed.Grid == 70 && committed.Local == 17 &&
            committed.Material == 0 && committed.Output == 1 && committed.DebitCalls == 1,
            "A committed product consumes its single 30-energy grid debit, never the local battery");
        Check(committed.RestoreCalls == 0 && committed.DisposeCalls == 1 && committed.FaultCalls == 0,
            "Committed external energy must not be refunded by receipt disposal");

        var directDraw = new NativeOperation();
        action = new NativePowerAction(directDraw.Plan);
        Check(action.Draw(30) && directDraw.PlanCalls == 1 && directDraw.DebitCalls == 1,
            "A native Draw without a preceding CanDraw must still acquire and verify a real receipt");
        directDraw.Work();
        Check(directDraw.Complete(action) && directDraw.Grid == 70 && directDraw.Local == 17,
            "Direct Draw and preflight-then-Draw must conserve the same energy");

        foreach (bool duplicateDraw in new[] { false, true })
        {
            var changed = new NativeOperation();
            action = new NativePowerAction(changed.Plan);
            Check(action.Prepare(30), "The first action cost must have a valid external reservation");
            bool rejected;
            if (duplicateDraw)
            {
                Check(action.Draw(30), "The first Draw must match the prepared cost");
                rejected = !action.Draw(30);
            }
            else rejected = !action.Draw(31);
            changed.Work(); // Native code may ignore a false Draw return value.
            Check(rejected && action.Failed && !action.Verify() && changed.Grid == 70 && changed.DebitCalls == 1,
                "A changed cost or second Draw must fail-stop without a second debit even if native work continues");
            Check(!changed.Complete(action) && changed.Refunded && changed.DisposeCalls == 1,
                "Rejected native work must compensate product, material and the original energy reservation");
        }

        var changedPreflight = new NativeOperation();
        action = new NativePowerAction(changedPreflight.Plan);
        Check(action.Prepare(30) && !action.Prepare(31) && action.Failed && changedPreflight.PlanCalls == 1,
            "Changing cost between two CanDraw calls must invalidate the existing reservation");
        Check(!changedPreflight.Complete(action) && changedPreflight.Refunded,
            "A changed CanDraw cost must refund its original reservation");

        foreach (Failure failure in new[] { Failure.NullPlan, Failure.PlanThrow, Failure.ValidateFalse, Failure.ValidateThrow })
        {
            var unavailable = new NativeOperation(failure);
            action = new NativePowerAction(unavailable.Plan);
            Check(!action.Draw(30) && action.Failed && unavailable.Grid == 100 && unavailable.Local == 17 &&
                unavailable.DebitCalls == 0,
                $"{failure}: unavailable or invalid receipts must reject before any native energy write");
            unavailable.Work();
            Check(!unavailable.Complete(action) && unavailable.Refunded,
                $"{failure}: native work that ignores rejection must not keep product or lose input");
            bool receiptExists = failure is Failure.ValidateFalse or Failure.ValidateThrow;
            Check(unavailable.DisposeCalls == (receiptExists ? 1 : 0),
                $"{failure}: only a receipt actually returned by the provider can be disposed");
        }

        foreach (Failure failure in new[] { Failure.DebitBeforeThrow, Failure.DebitAfterThrow, Failure.VerifyFalse, Failure.VerifyThrow })
        {
            var interrupted = new NativeOperation(failure);
            action = new NativePowerAction(interrupted.Plan);
            Check(!action.Prepare(30) && action.Failed && interrupted.DebitCalls == 1,
                $"{failure}: failed debit or immediate readback must reject the reservation");
            Check(interrupted.Grid == (failure == Failure.DebitBeforeThrow ? 100 : 70) && interrupted.Local == 17,
                $"{failure}: the fixture must distinguish a failure before a debit from one after an actual write");
            interrupted.Work();
            Check(!interrupted.Complete(action) && interrupted.Refunded && interrupted.RestoreCalls == 1 &&
                interrupted.DisposeCalls == 1,
                $"{failure}: compensation must cover a throwing debit's possible write and release its receipt");
        }

        foreach (Failure failure in new[] { Failure.VerifyFalse, Failure.VerifyThrow })
        {
            var lateReadback = new NativeOperation(failure, verifyFailureAt: 3);
            action = new NativePowerAction(lateReadback.Plan);
            Check(action.Prepare(30) && action.Draw(30) && lateReadback.Grid == 70,
                "A final readback failure must occur after both successful reservation checks and native writes");
            lateReadback.Work();
            Check(!lateReadback.Complete(action) && action.Failed && lateReadback.Refunded &&
                lateReadback.DisposeCalls == 1,
                $"{failure}: failed final readback must refund energy and undo completed native work");
        }

        var drift = new NativeOperation();
        action = new NativePowerAction(drift.Plan);
        Check(action.Prepare(30), "Energy drift needs an initially valid reservation");
        drift.Grid--;
        Check(!action.Prepare(30) && action.Failed && drift.DebitCalls == 1,
            "Repeated CanDraw must read the existing receipt and reject changed energy without replanning");
        Check(!drift.Complete(action) && drift.Grid == 69 && drift.Local == 17 && drift.Material == 6 &&
            drift.Output == 0 && drift.FaultCalls == 1 && drift.DisposeCalls == 1,
            "Unrecognized grid changes must remain uncertain rather than overwrite another write during compensation");

        foreach (bool draw in new[] { false, true })
        {
            var free = new NativeOperation(Failure.PlanThrow);
            action = new NativePowerAction(free.Plan);
            Check(action.Prepare(0) && action.HasReservation && free.PlanCalls == 0 && free.DebitCalls == 0,
                "Zero cost must not ask an external provider to create a transaction");
            if (draw) Check(action.Draw(0), "A matching zero-cost Draw must succeed without a receipt");
            free.Work();
            Check(free.Complete(action) == draw && free.Grid == 100 && free.Local == 17 &&
                free.Material == (draw ? 0 : 6) && free.Output == (draw ? 1 : 0) && free.DisposeCalls == 0,
                "Zero cost still needs matching Draw confirmation, and never debits or disposes a provider receipt");
        }

        foreach (Failure failure in new[] { Failure.RestoreFalse, Failure.RestoreThrow })
        {
            var uncertain = new NativeOperation(failure);
            action = new NativePowerAction(uncertain.Plan);
            Check(action.Prepare(30), "Uncertain rollback starts with an actual grid debit");
            uncertain.Work();
            Check(!uncertain.Complete(action) && uncertain.Grid == 70 && uncertain.Local == 17 &&
                uncertain.Material == 6 && uncertain.Output == 0,
                $"{failure}: failed grid compensation must not prevent restoration of other native resources");
            Check(uncertain.FaultCalls == 1 && uncertain.DisposeCalls == 1 && uncertain.LocalRestoreCalls == 1 &&
                uncertain.LastFault == "native batch rollback uncertain",
                $"{failure}: uncertain external energy must fault its owner and release the receipt");
        }

        foreach (bool commit in new[] { false, true })
        {
            var cleanupFailure = new NativeOperation(Failure.DisposeThrow);
            action = new NativePowerAction(cleanupFailure.Plan);
            Check(action.Prepare(30) && (!commit || action.Draw(30)),
                "Disposal failure must be tested after a real prepared or drawn external debit");
            cleanupFailure.Work();
            Check(cleanupFailure.Complete(action) == commit && cleanupFailure.DisposeCalls == 1 &&
                cleanupFailure.FaultCalls == 1 && cleanupFailure.LastFault!.StartsWith("native batch cleanup failed:"),
                "A throwing Dispose must fault the provider without changing the commit decision or skipping cleanup");
            Check(cleanupFailure.Grid == (commit ? 70 : 100) && cleanupFailure.Local == 17 &&
                cleanupFailure.Material == (commit ? 0 : 6) && cleanupFailure.Output == (commit ? 1 : 0),
                "Cleanup failure must preserve a confirmed debit, or preserve the already verified rollback");
        }

        Console.WriteLine($"Native power reservations and native/grid conservation: {checks} assertions passed (offline boundary).");
    }

    private enum Failure
    {
        None, NullPlan, PlanThrow, ValidateFalse, ValidateThrow, DebitBeforeThrow, DebitAfterThrow,
        VerifyFalse, VerifyThrow, RestoreFalse, RestoreThrow, DisposeThrow
    }

    // Models actual energy and native input/output writes, not the action's state machine.
    private sealed class NativeOperation(Failure failure = Failure.None, int verifyFailureAt = 1)
    {
        internal int Grid = 100, Local = 17, Material = 6, Output;
        internal int PlanCalls, ValidateCalls, DebitCalls, VerifyCalls, RestoreCalls, DisposeCalls, FaultCalls, LocalRestoreCalls;
        internal string? LastFault;
        internal bool Refunded => Grid == 100 && Local == 17 && Material == 6 && Output == 0;

        internal IForgePowerTransaction? Plan(int cost)
        {
            PlanCalls++;
            if (failure == Failure.NullPlan) return null;
            if (failure == Failure.PlanThrow) throw new InvalidOperationException("provider unavailable");
            return new Receipt(this, cost, failure, verifyFailureAt);
        }

        internal void Work() { Material = 0; Output = 1; }

        internal bool Complete(NativePowerAction action) => NativeBatchFinalization.Complete(action.Verify,
        [
            () => { Output = 0; return true; },
            () => { Material = 6; return true; },
            action.Restore,
            () => { LocalRestoreCalls++; Local = 17; return true; }
        ], action.Fault, action.Dispose);

        private sealed class Receipt(NativeOperation operation, int cost, Failure failure, int verifyFailureAt) : IForgePowerTransaction
        {
            private readonly int _before = operation.Grid;
            public bool Validate()
            {
                operation.ValidateCalls++;
                if (failure == Failure.ValidateThrow) throw new InvalidOperationException("invalid native handle");
                return failure != Failure.ValidateFalse && operation.Grid == _before && _before >= cost;
            }
            public void Debit()
            {
                operation.DebitCalls++;
                if (failure == Failure.DebitBeforeThrow) throw new InvalidOperationException("native debit before write");
                operation.Grid -= cost;
                if (failure == Failure.DebitAfterThrow) throw new InvalidOperationException("native debit after write");
            }
            public bool Verify()
            {
                operation.VerifyCalls++;
                if (operation.VerifyCalls >= verifyFailureAt)
                {
                    if (failure == Failure.VerifyThrow) throw new InvalidOperationException("energy readback unavailable");
                    if (failure == Failure.VerifyFalse) return false;
                }
                return operation.Grid == _before - cost;
            }
            public bool Restore()
            {
                operation.RestoreCalls++;
                if (failure == Failure.RestoreThrow) throw new InvalidOperationException("native restore unavailable");
                if (failure == Failure.RestoreFalse) return false;
                if (operation.Grid != _before && operation.Grid != _before - cost) return false;
                operation.Grid = _before;
                return operation.Grid == _before;
            }
            public void Fault(string reason) { operation.FaultCalls++; operation.LastFault = reason; }
            public void Dispose()
            {
                operation.DisposeCalls++;
                if (failure == Failure.DisposeThrow) throw new InvalidOperationException("receipt release unavailable");
            }
        }
    }
}
