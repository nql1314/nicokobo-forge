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
    var existingPhases = new[] { ForgeLifecyclePhase.BeforeLoad, ForgeLifecyclePhase.AfterLoad,
        ForgeLifecyclePhase.BeforeNight, ForgeLifecyclePhase.AfterNight, ForgeLifecyclePhase.BeforeNightServices,
        ForgeLifecyclePhase.AfterNightServices, ForgeLifecyclePhase.AfterSleep, ForgeLifecyclePhase.AfterEndDay };
    Require(existingPhases.Select(phase => (int)phase).SequenceEqual(Enumerable.Range(0, 8)) &&
        (int)ForgeLifecyclePhase.NightAttemptFinished == 8 && (int)ForgeLifecyclePhase.AfterGameLoadedLate == 9,
        "Adding lifecycle notifications changed existing enum values");
    var observed = Enum.GetValues<ForgeLifecyclePhase>().ToDictionary(phase => phase, _ => 0);
    var order = new List<ForgeLifecyclePhase>();
    var leases = new List<IDisposable>();
    foreach (var phase in observed.Keys)
        leases.Add(ForgeLifecycleApi.Subscribe("nicokobo.lifecycle.check", "nicokobo.lifecycle.check." + phase.ToString().ToLowerInvariant(),
            phase, context =>
            {
                Require(context.Store.Pointer == store.Pointer && context.Run == new ForgeRunIdentity("lifecycle-run", 2, 7),
                    "Lifecycle callback saw a different live run");
                observed[phase]++;
                order.Add(phase);
            }));
    var boundaries = new (Type Type, string Name, ForgeLifecyclePhase? Before, ForgeLifecyclePhase? After)[]
    {
        (Type: typeof(PlayerStore), Name: nameof(PlayerStore.LoadGame), Before: (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeLoad, After: (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterLoad),
        (typeof(PlayerStore), nameof(PlayerStore.EndNight), (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeNight, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterNight),
        (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesEarly), (ForgeLifecyclePhase?)ForgeLifecyclePhase.BeforeNightServices, (ForgeLifecyclePhase?)null),
        (typeof(ModHook), nameof(ModHook.FireOnHandlingNightlyServicesLate), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterNightServices),
        (typeof(ModHook), nameof(ModHook.FireOnGoingSleepLate), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterSleep),
        (typeof(PlayerStore), nameof(PlayerStore.EndDay), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterEndDay),
        (typeof(ModHook), nameof(ModHook.FireOnGameLoadedLate), (ForgeLifecyclePhase?)null, (ForgeLifecyclePhase?)ForgeLifecyclePhase.AfterGameLoadedLate)
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
        if (boundary.Name == nameof(PlayerStore.EndNight))
            Require(observed[ForgeLifecyclePhase.NightAttemptFinished] == 1,
                "Rejected night did not finish its attempt cleanup");
        Boundary.Call(boundary.Type, boundary.Name, store, runOriginal: true);
        Require(store.BodyCalls + ModHook.BodyCalls == beforeBodies + 1, "Successful boundary did not execute exactly one body");
        if (boundary.Before is { } completedBefore)
            Require(observed[completedBefore] == 2, "Successful native call lost its Before event: " + completedBefore);
        if (boundary.After is { } completedAfter)
            Require(observed[completedAfter] == 1, "Successful native body did not dispatch exactly one After event: " + completedAfter);
        if (boundary.Name == nameof(PlayerStore.EndNight))
        {
            Require(observed[ForgeLifecyclePhase.NightAttemptFinished] == 2, "Executed night did not finish its attempt cleanup");
            Require(order.TakeLast(2).SequenceEqual(new[] { ForgeLifecyclePhase.AfterNight, ForgeLifecyclePhase.NightAttemptFinished }),
                "Night attempt cleanup ran before the executed night's business After callback");
        }
        if (boundary.Name == nameof(PlayerStore.LoadGame))
            Require(observed[ForgeLifecyclePhase.AfterGameLoadedLate] == 0,
                "LoadGame body return fabricated the precise native game-loaded-late event");
    }
    foreach (var lease in leases) lease.Dispose();
    int standaloneCleanup = 0;
    using (var cleanup = ForgeLifecycleApi.Subscribe("nicokobo.lifecycle.check", "nicokobo.lifecycle.check.cleanup_only",
        ForgeLifecyclePhase.NightAttemptFinished, _ => standaloneCleanup++))
    {
        Require(NativeHookSet.Installed.Count == 1 && NativeHookSet.Installed.Values.SelectMany(hooks => hooks).All(hook => hook.TargetType == typeof(PlayerStore) && hook.Method == nameof(PlayerStore.EndNight)),
            "Standalone night cleanup subscription did not install the native night boundary");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, false);
        Require(standaloneCleanup == 1, "Standalone night cleanup subscription did not observe a rejected attempt");
    }
    int standaloneLate = 0;
    using (var late = ForgeLifecycleApi.Subscribe("nicokobo.lifecycle.check", "nicokobo.lifecycle.check.late_only",
        ForgeLifecyclePhase.AfterGameLoadedLate, _ => standaloneLate++))
    {
        Require(NativeHookSet.Installed.Count == 1 && NativeHookSet.Installed.Values.SelectMany(hooks => hooks).All(hook => hook.TargetType == typeof(ModHook) && hook.Method == nameof(ModHook.FireOnGameLoadedLate)),
            "Standalone game-loaded-late subscription installed the wrong native boundary");
        Boundary.Call(typeof(ModHook), nameof(ModHook.FireOnGameLoadedLate), store, false);
        Require(standaloneLate == 0, "Standalone game-loaded-late subscription observed a rejected body");
        Boundary.Call(typeof(ModHook), nameof(ModHook.FireOnGameLoadedLate), store, true);
        Require(standaloneLate == 1, "Standalone game-loaded-late subscription lost the actual late notification");
    }
    MachineLifecycleChecks.Run(Require);
    PresentationChecks.Run(Require);
    Console.WriteLine($"Lifecycle and presentation checks passed: {checks} assertions.");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
