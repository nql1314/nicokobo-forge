using Nicokobo.Forge;

internal static class NativeBatchFinalizationChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        foreach (bool persistentlyThrows in new[] { false, true })
        {
            int material = 0, output = 1, energy = 0, calls = 0;
            bool active = true, disposed = false, faulted = false;
            var receipt = new ThrowingReadback(persistentlyThrows);
            Check(receipt.Verify(), "Native debit's first verification succeeds before material/output changes");
            bool committed = NativeBatchFinalization.Complete(() => receipt.Verify(),
            [() => { output = 0; calls++; return true; }, () => { material = 6; calls++; return true; }, () => { energy = 80; calls++; return true; }],
                _ => faulted = true, () => { active = false; disposed = true; });
            Check(!committed && output == 0 && material == 6 && energy == 80 && calls == 3,
                "A final once/persistently throwing Verify must restore all already-written native resources");
            Check(faulted && disposed && !active, "Throwing final readback must fail-stop, dispose and release thread batch context");
        }
        var trace = new List<string>(); bool cleaned = false, fault = false;
        NativeBatchFinalization.Complete(() => throw new Exception("native handle invalid"),
            [() => { trace.Add("output"); throw new Exception("output invalid"); }, () => { trace.Add("input"); return true; }, () => { trace.Add("energy"); return true; }],
            _ => { fault = true; throw new Exception("fault callback unavailable"); }, () => cleaned = true);
        Check(trace.SequenceEqual(new[] { "output", "input", "energy" }) && cleaned && fault,
            "A broken compensation/fault callback must not skip other resources or final cleanup");
        trace.Clear(); cleaned = false;
        Check(NativeBatchFinalization.Complete(() => true, [() => { trace.Add("unexpected rollback"); return true; }], _ => { }, () => cleaned = true) &&
            cleaned && trace.Count == 0, "A verified committed batch releases its lease without restoring valid writes");
        int identity = 41, units = 0; bool attached = true, destroyed = false, rejected = false;
        NativeBatchFinalization.DetachForDeferredDestruction(() => identity == 41, () => !attached,
            () => { attached = false; return true; }, _ => rejected = true);
        Check(!attached && !destroyed && identity == 41 && !rejected,
            "Whole-stack consumption must expel the original while retaining its UID and destruction body for commit");
        Check(!NativeBatchFinalization.Complete(() => false,
            [() => { units = 6; attached = true; return identity == 41; }], _ => { }, () => { }) &&
            attached && units == 6 && identity == 41 && !destroyed,
            "Deferred whole-stack failure must restore the same instance and units without running destruction");
        attached = true; rejected = false;
        NativeBatchFinalization.DetachForDeferredDestruction(() => false, () => !attached,
            () => { attached = false; return true; }, _ => rejected = true);
        Check(attached && rejected, "Changed UID or custody must reject before touching another instance or inventory");
        rejected = false;
        NativeBatchFinalization.DetachForDeferredDestruction(() => true, () => !attached,
            () => throw new Exception("native expel readback"), _ => rejected = true);
        Check(attached && rejected, "A throwing native detach must reject the batch while destruction remains deferred");
        rejected = false;
        NativeBatchFinalization.DetachForDeferredDestruction(() => true, () => !attached,
            () => true, _ => rejected = true);
        Check(attached && rejected, "A successful Expel return without actual custody change must reject the batch");
        Console.WriteLine($"Native batch final readback and unconditional cleanup: {checks} assertions passed (offline boundary).");
    }
    private sealed class ThrowingReadback(bool persistent)
    {
        private int _calls;
        internal bool Verify() => ++_calls == 1 || !persistent && _calls > 2 ? true : throw new Exception("injected Verify readback");
    }
}
