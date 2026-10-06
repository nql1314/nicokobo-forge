using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

try
{
    int checks = 0;
    void Require(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException(reason); }
    var store = PlayerStore.Instance;
    ForgeLifecycleApi.Configure(true, Console.WriteLine);
    var observed = Enum.GetValues<ForgeLifecyclePhase>().ToDictionary(phase => phase, _ => 0);
    var leases = new List<IDisposable>();
    foreach (var phase in observed.Keys)
        leases.Add(ForgeLifecycleApi.Subscribe("nicokobo.lifecycle.check", "nicokobo.lifecycle.check." + phase.ToString().ToLowerInvariant(),
            phase, context =>
            {
                Require(context.Store.Pointer == store.Pointer && context.Run == new ForgeRunIdentity("lifecycle-run", 2, 7),
                    "Lifecycle callback saw a different live run");
                observed[phase]++;
            }));
    var boundaries = new (Type Type, string Name, ForgeLifecyclePhase? Before, ForgeLifecyclePhase? After)[]
    {
        (Type: typeof(PlayerStore), Name: nameof(PlayerStore.LoadGame), Before: (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeLoad, After: (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterLoad),
        (typeof(PlayerStore), nameof(PlayerStore.EndNight), (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeNight, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterNight),
        (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesEarly), (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeNightServices, (ForgeLifecyclePhase?)null),
        (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesLate), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterNightServices),
        (typeof(ModHook), nameof(ModHook.FireOnGoingSleepLate), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterSleep),
        (typeof(PlayerStore), nameof(PlayerStore.EndDay), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterEndDay)
    };
    foreach (var boundary in boundaries)
    {
        int beforeBodies = store.BodyCalls + ModHook.BodyCalls;
        Boundary.Call(boundary.Type, boundary.Name, store, runOriginal: false);
        Require(store.BodyCalls + ModHook.BodyCalls == beforeBodies, "A rejected boundary executed its original body");
        if (boundary.Before is { } before)
            Require(observed[before] == 1, "Rejected native call lost its Before intent event: " + before);
        if (boundary.After is { } after)
            Require(observed[after] == 0, "Rejected native body dispatched an After event: " + after);
        Boundary.Call(boundary.Type, boundary.Name, store, runOriginal: true);
        Require(store.BodyCalls + ModHook.BodyCalls == beforeBodies + 1, "Successful boundary did not execute exactly one body");
        if (boundary.Before is { } completedBefore)
            Require(observed[completedBefore] == 2, "Successful native call lost its Before event: " + completedBefore);
        if (boundary.After is { } completedAfter)
            Require(observed[completedAfter] == 1, "Successful native body did not dispatch exactly one After event: " + completedAfter);
    }
    foreach (var lease in leases) lease.Dispose();
    MachineLifecycleChecks.Run(Require);
    Console.WriteLine($"Lifecycle checks passed: {checks} assertions (five rejected/executed After boundaries, Before intent and machine load ledger).");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
