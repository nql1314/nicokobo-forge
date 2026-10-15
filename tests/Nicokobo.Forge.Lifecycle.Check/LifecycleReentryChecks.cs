using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class LifecycleReentryChecks
{
    internal static void Run(Action<bool, string> require)
    {
        var store = PlayerStore.instance!;
        void Call(string method) => Boundary.Call(typeof(ModHook), method, store, true);
        var boundaries = new[]
        {
            (ForgeLifecyclePhase.BeforeNightServices, nameof(ModHook.FireOnHandlingNightlyServicesEarly)),
            (ForgeLifecyclePhase.AfterNightServices, nameof(ModHook.FireOnHandlingNightlyServicesLate)),
            (ForgeLifecyclePhase.AfterSleep, nameof(ModHook.FireOnGoingSleepLate)),
            (ForgeLifecyclePhase.AfterGameLoadedLate, nameof(ModHook.FireOnGameLoadedLate))
        };
        try
        {
            foreach (var (phase, method) in boundaries)
            {
                int callbacks = 0;
                using var lease = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.missing", phase, _ => callbacks++);
                PlayerStore.instance = null;
                PlayerStore.SingletonReads = 0;
                Call(method);
                require(PlayerStore.instance == null && PlayerStore.SingletonReads == 0 && callbacks == 0,
                    "Passive lifecycle notification constructed or requested a store: " + phase);
                PlayerStore.instance = store;
            }

            const string early = nameof(ModHook.FireOnHandlingNightlyServicesEarly);
            int observations = 0, followers = 0, bodies = ModHook.BodyCalls;
            using (var observer = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.observer",
                ForgeLifecyclePhase.BeforeNightServices, _ =>
                {
                    // Bound the broken implementation instead of causing a real stack overflow.
                    if (++observations <= 4) Call(early);
                }))
            using (var follower = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.follower",
                ForgeLifecyclePhase.BeforeNightServices, _ => followers++, 1))
            {
                Call(early);
                require(observations == 1 && followers == 1,
                    "A subscriber recursively redispatched the same lifecycle phase and store");
                require(ModHook.BodyCalls == bodies + 2,
                    "Observation protection suppressed a native body instead of only the repeated notification");
                Call(early);
                require(observations == 2 && followers == 2,
                    "A later independent lifecycle notification remained blocked");
            }

            int dayReads = 0, captured = 0;
            using (var observer = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.context",
                ForgeLifecyclePhase.BeforeNightServices, _ => captured++))
            {
                StoreStation.BeforeDayRead = () => { if (++dayReads <= 4) Call(early); };
                try { Call(early); }
                finally { StoreStation.BeforeDayRead = null; }
                require(dayReads == 1 && captured == 1,
                    "Context construction reentered lifecycle dispatch before its protection was active");
            }

            int otherPhase = 0;
            using (var sleep = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.sleep",
                ForgeLifecyclePhase.AfterSleep, _ => otherPhase++))
            using (var earlyObserver = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.cross_phase",
                ForgeLifecyclePhase.BeforeNightServices, _ => Call(nameof(ModHook.FireOnGoingSleepLate))))
            {
                Call(early);
                require(otherPhase == 1, "A different nested lifecycle phase was suppressed");
            }

            var otherStore = new PlayerStore { Pointer = (IntPtr)99, runID = "other-run" };
            var stores = new List<IntPtr>();
            using (var observer = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.cross_store",
                ForgeLifecyclePhase.BeforeNightServices, context =>
                {
                    stores.Add(context.Store.Pointer);
                    if (context.Store.Pointer != store.Pointer) return;
                    PlayerStore.instance = otherStore;
                    try { Call(early); }
                    finally { PlayerStore.instance = store; }
                }))
            {
                Call(early);
                require(stores.SequenceEqual(new[] { store.Pointer, otherStore.Pointer }),
                    "A separate store's nested lifecycle notification was suppressed");
            }

            int failingCalls = 0;
            using (var observer = ForgeLifecycleApi.Subscribe("test.reentry", "test.reentry.failure",
                ForgeLifecyclePhase.BeforeNightServices, _ =>
                { failingCalls++; throw new InvalidOperationException("injected observer failure"); }))
            {
                Call(early); Call(early);
                require(failingCalls == 2, "An observer exception left lifecycle dispatch permanently blocked");
            }
        }
        finally
        {
            PlayerStore.instance = store;
            StoreStation.BeforeDayRead = null;
        }
    }
}
