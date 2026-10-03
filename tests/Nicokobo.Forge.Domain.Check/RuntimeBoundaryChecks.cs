using Nicokobo.Forge.Runtime;
using Nicokobo.Forge.Registration;

static class RuntimeBoundaryChecks
{
    internal static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string message)
        { assertions++; if (!condition) throw new Exception(message); }
        void Throws(Action action, string message)
        {
            bool threw = false; try { action(); } catch { threw = true; }
            Check(threw, message);
        }
        var callbacks = new OwnedCallbacks<string, List<string>>();
        Throws(() => callbacks.Add("foreign", "mine.event", "night", _ => { }), "Invalid owner accepted");
        Throws(() => callbacks.Add("test.one", "test.two.event", "night", _ => { }), "Foreign callback accepted");
        var a = callbacks.Add("test.one", "test.one.a", "night", x => x.Add("a"));
        var b = callbacks.Add("test.two", "test.two.b", "night", x => x.Add("b"), -1);
        var fault = callbacks.Add("test.fault", "test.fault.event", "night", _ => throw new Exception("injected"), -2);
        var other = callbacks.Add("test.one", "test.one.load", "load", x => x.Add("load"));
        Throws(() => callbacks.Add("test.one", "test.one.a", "night", _ => { }), "Callback ID replaced");
        var trace = new List<string>();
        callbacks.Dispatch("night", trace, _ => throw new Exception("logger"));
        Check(trace.SequenceEqual(new[] { "b", "a" }), "Owner failure or logger failure changed dispatch");
        Check(callbacks.Has("night") && callbacks.Has("load"), "Signals not independent");
        b.Dispose(); b.Dispose(); trace.Clear(); callbacks.Dispatch("night", trace);
        Check(trace.SequenceEqual(new[] { "a" }), "Disposal failed or removed another owner");
        a.Dispose(); fault.Dispose(); other.Dispose();
        Check(!callbacks.Has("night") && !callbacks.Has("load"), "Callback leases leaked");
        using var readd = callbacks.Add("test.one", "test.one.a", "night", x => x.Add("fresh"));
        trace.Clear(); callbacks.Dispatch("night", trace);
        Check(trace.SequenceEqual(new[] { "fresh" }), "Disposed ID cannot be reused");
        var during = new OwnedCallbacks<int, List<int>>();
        IDisposable? late = null;
        using var first = during.Add("test.one", "test.one.first", 1, x =>
        { x.Add(1); late ??= during.Add("test.two", "test.two.late", 1, y => y.Add(2)); });
        var numbers = new List<int>(); during.Dispatch(1, numbers);
        Check(numbers.SequenceEqual(new[] { 1 }), "Dispatch did not retain stable snapshot");
        numbers.Clear(); during.Dispatch(1, numbers);
        Check(numbers.SequenceEqual(new[] { 1, 2 }), "Deferred subscriber missing next dispatch");
        late!.Dispose();
        using var nested = during.Add("test.one", "test.one.nested", 2, x => during.Dispatch(1, x));
        numbers.Clear(); during.Dispatch(2, numbers);
        Check(numbers.SequenceEqual(new[] { 1 }), "Nested dispatch broken");

        var released = new OwnedCallbacks<int, List<int>>();
        IDisposable? removed = null;
        IDisposable? replacement = null;
        using var remover = released.Add("test.one", "test.one.remover", 1, x =>
        {
            x.Add(1);
            removed!.Dispose();
            replacement ??= released.Add("test.one", "test.one.removed", 1, y => y.Add(3), 1);
        });
        removed = released.Add("test.one", "test.one.removed", 1, x => x.Add(2), 1);
        numbers.Clear(); released.Dispatch(1, numbers);
        Check(numbers.SequenceEqual(new[] { 1 }), "Disposed callback ran from an in-flight snapshot");
        numbers.Clear(); released.Dispatch(1, numbers);
        Check(numbers.SequenceEqual(new[] { 1, 3 }), "Reused callback ID was removed by an old lease");
        replacement!.Dispose();

        var frequent = new OwnedCallbacks<int, object>();
        using var observer = frequent.Add("test.one", "test.one.observe", 1, _ => { });
        var context = new object();
        for (int i = 0; i < 100; i++) frequent.Dispatch(1, context);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) frequent.Dispatch(1, context);
        Check(GC.GetAllocatedBytesForCurrentThread() - allocated == 0,
            "Unchanged callback dispatch allocated or rebuilt its sorted snapshot");

        var admission = new ModuleAdmissionCatalog();
        int installs = 0;
        Check(admission.Submit("test.one", [new("furnace", ["foreign.type"])], () => { installs++; return true; }).Status == SubmitStatus.Invalid,
            "Foreign module type accepted");
        Check(installs == 0, "Invalid rule installed a native hook");
        string[] callerTypes = ["test.one.type.furnace"];
        Check(admission.Submit("test.one", [new("furnace", callerTypes)], () => false).Status == SubmitStatus.Invalid, "Adapter failure accepted");
        Check(admission.Merge("furnace", ["native"]).SequenceEqual(new[] { "native" }), "Adapter failure published rules");
        Check(admission.Submit("test.one", [new("furnace", callerTypes)]).Status == SubmitStatus.Accepted, "Admission rejected");
        callerTypes[0] = "mutated";
        Check(admission.Merge("furnace", ["native", "native"]).SequenceEqual(new[] { "native", "test.one.type.furnace" }), "Native types lost or caller list was retained");
        Check(admission.Submit("test.one", [new("furnace", ["test.one.type.furnace"])]).Status == SubmitStatus.AlreadyPresent, "Admission not idempotent");
        Check(admission.Submit("test.two", [new("furnace", ["test.two.type.furnace"])]).Status == SubmitStatus.Accepted, "Second owner cannot extend a bay");
        Check(admission.Merge("furnace", ["native"]).SequenceEqual(new[] { "native", "test.one.type.furnace", "test.two.type.furnace" }), "Owner additions not deterministic");
        Check(admission.Submit("test.one", [new("purifier", ["test.one.type.purifier"]), new("furnace", ["test.one.type.changed"])]).Status == SubmitStatus.Conflict, "Batch conflict ignored");
        Check(admission.Merge("purifier", ["native"]).SequenceEqual(new[] { "native" }), "Conflicting batch partially published");
        Check(admission.Merge("unrelated", ["universal"]).SequenceEqual(new[] { "universal" }), "Unrelated machine altered");
        Check(admission.Submit("test.one", [new("furnace", ["test.one.type.a", "test.one.type.a"])]).Status == SubmitStatus.Invalid, "Duplicate type accepted");
        Check(admission.Submit("test.one", [new("a", ["test.one.type.a"]), new("a", ["test.one.type.b"])]).Status == SubmitStatus.Invalid, "Duplicate machine accepted");

        for (int energy = 0; energy <= 6; energy++)
        for (int cost = 0; cost <= 7; cost++)
        {
            int balance = energy, calls = 0;
            var result = EnergyDebit.Execute(cost, () => balance, amount => { calls++; balance -= amount; return amount; }, before => { balance = before; return true; });
            Check(result.Status == (energy >= cost ? ForgeEnergyDebitStatus.Committed : ForgeEnergyDebitStatus.Insufficient) &&
                balance == (energy >= cost ? energy - cost : energy) && calls == (cost > 0 && energy >= cost ? 1 : 0), "Debit balance or insufficient preflight incorrect");
        }
        int power = 30;
        var partial = EnergyDebit.Execute(15, () => power, _ => { power -= 4; return 4; }, before => { power = before; return true; });
        Check(partial.Status == ForgeEnergyDebitStatus.Restored && power == 30, "Partial debit not compensated");
        var exception = EnergyDebit.Execute(15, () => power, _ => { power -= 4; throw new Exception("after write"); }, before => { power = before; return true; });
        Check(exception.Status == ForgeEnergyDebitStatus.Restored && power == 30, "Throw after write not compensated");
        var refused = EnergyDebit.Execute(15, () => power, _ => 0, _ => throw new Exception("must not restore unchanged"));
        Check(refused.Status == ForgeEnergyDebitStatus.Restored && power == 30, "Unchanged refusal attempted a write");
        var badReceipt = EnergyDebit.Execute(15, () => power, _ => { power -= 15; return 0; }, before => { power = before; return true; });
        Check(badReceipt.Status == ForgeEnergyDebitStatus.Restored && power == 30, "Wrong native receipt reported success");
        var faulted = EnergyDebit.Execute(15, () => power, _ => { power -= 4; return 4; }, _ => false);
        Check(faulted.Status == ForgeEnergyDebitStatus.Faulted && power == 26 && faulted.After == 26, "Restore failure hidden");
        var badRead = EnergyDebit.Execute(15, () => throw new Exception("unavailable"), _ => throw new Exception("must not debit"), _ => false);
        Check(badRead.Status == ForgeEnergyDebitStatus.Invalid, "Invalid read attempted a debit");
        Check(EnergyDebit.Execute(-1, () => power, _ => 0, _ => false).Status == ForgeEnergyDebitStatus.Invalid, "Negative debit accepted");
        Console.WriteLine($"Runtime boundaries: {assertions} assertions passed (owner isolation, stable dispatch, admission, energy faults).");
    }
}
