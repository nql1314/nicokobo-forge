using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Nicokobo.Forge.Runtime;

internal static class HookBridgeChecks
{
    private static int _prefixA, _postfixA, _prefixB, _postfixB, _prefixC, _postfixC, _nestedResult;

    internal static int Run(RecordingDetourProvider provider, bool baseline)
    {
        int checks = 0;
        void Expect(bool value, string reason)
        {
            if (!value) throw new Exception(reason);
            checks++;
        }

        var backend = Type.GetType("Il2CppInterop.HarmonySupport.Il2CppDetourMethodPatcher, Il2CppInterop.HarmonySupport", true)!;
        var rebuild = AccessTools.DeclaredMethod(backend, "DetourTo", [typeof(MethodBase)]);
        var original = PatchProcessor.GetOriginalInstructions(rebuild);
        bool Affected(IEnumerable<CodeInstruction> codes) => codes.Any(code => code.opcode == OpCodes.Newobj &&
            code.operand is ConstructorInfo constructor && constructor.DeclaringType?.FullName == "MonoMod.RuntimeDetour.Detour");
        bool originallyAffected = Affected(original);
        Expect(!baseline || originallyAffected, "This loader no longer contains the affected backend for the baseline run");
        if (!baseline)
        {
            Expect(Il2CppHookCompatibility.Install(Console.WriteLine), "Compatibility installation failed");
            Expect(Il2CppHookCompatibility.Install(Console.WriteLine), "Compatibility installation was not idempotent");
        }
        Expect(!Affected(PatchProcessor.GetCurrentInstructions(rebuild)),
            "Affected backend still installs a managed Detour before the next CopyOriginal");
        Expect((Harmony.GetPatchInfo(rebuild)?.Transpilers.Count(patch => patch.owner == "nicokobo.forge.il2cpp_hook_compatibility") ?? 0) == (originallyAffected ? 1 : 0),
            "Repeated installation duplicated the backend repair");
        var fixedBody = Il2CppHookCompatibility.RemoveManagedDetour(original.Select(code => new CodeInstruction(code)).ToList());
        Expect(!Affected(fixedBody), "Backport did not remove the managed-only detour");
        Expect(fixedBody.Any(code => code.operand is MethodInfo { Name: "get_OriginalTrampoline" }),
            "Backport removed the native original trampoline");
        Expect(Il2CppHookCompatibility.RemoveManagedDetour(fixedBody).Count == fixedBody.Count,
            "Already-fixed backend was modified a second time");
        var changed = original.Select(code => new CodeInstruction(code)).ToList();
        int ctor = changed.FindIndex(code => Affected([code]));
        if (ctor >= 0)
        {
            changed[ctor + 3].operand = typeof(HookBridgeChecks).GetMethod(nameof(PrefixA), BindingFlags.NonPublic | BindingFlags.Static);
            bool rejected = false;
            try { Il2CppHookCompatibility.RemoveManagedDetour(changed); }
            catch (InvalidOperationException) { rejected = true; }
            Expect(rejected && Affected(changed), "Unrecognized backend must be rejected without a partial rewrite");
        }

        var countInfo = UnityVersionHandler.NewMethod();
        var amountInfo = UnityVersionHandler.NewMethod();
        countInfo.MethodPointer = provider.Original;
        amountInfo.MethodPointer = new IntPtr(2);
        Il2Cpp.GameItem.NativeMethodInfoPtr_SetUnitCount = countInfo.Pointer;
        Il2Cpp.GameItem.NativeMethodInfoPtr_SetAmount = amountInfo.Pointer;
        var count = AccessTools.DeclaredMethod(typeof(Il2Cpp.GameItem), nameof(Il2Cpp.GameItem.SetUnitCount));
        var amount = AccessTools.DeclaredMethod(typeof(Il2Cpp.GameItem), nameof(Il2Cpp.GameItem.SetAmount));
        Expect(Il2CppHookCompatibility.QuantityTarget(count) == count,
            "Distinct native setters were merged");
        amountInfo.MethodPointer = IntPtr.Zero;
        Expect(Il2CppHookCompatibility.QuantityTarget(count) == count, "Zero native entry was merged");
        amountInfo.MethodPointer = provider.Original;
        Expect(Il2CppHookCompatibility.QuantityTarget(count) == amount && Il2CppHookCompatibility.QuantityTarget(amount) == amount,
            "Native aliases did not resolve to one canonical method");
        Expect(Il2CppHookCompatibility.QuantityTarget(typeof(Fixture).GetMethod(nameof(Fixture.Valid))!)?.Name == nameof(Fixture.Valid),
            "Unrelated native methods were redirected");

        void Resolve(object? sender, PatchManager.PatcherResolverEventArgs e)
        {
            if (e.Original.DeclaringType == typeof(Il2Cpp.GameItem))
                e.MethodPatcher = (MethodPatcher)Activator.CreateInstance(backend, [e.Original])!;
        }
        PatchManager.ResolvePatcher += Resolve;
        BridgeFixture.Provider = provider;
        var ownerA = new Harmony("fixture.quantity.cache");
        var ownerB = new Harmony("fixture.quantity.workshop");
        var ownerC = new Harmony("fixture.quantity.external");
        try
        {
            Expect(ReferenceEquals(count.GetMethodPatcher(), amount.GetMethodPatcher()),
                "Aliases created independent native patchers");
            var prefixA = new HarmonyMethod(typeof(HookBridgeChecks), nameof(PrefixA));
            var postfixA = new HarmonyMethod(typeof(HookBridgeChecks), nameof(PostfixA));
            var prefixB = new HarmonyMethod(typeof(HookBridgeChecks), nameof(PrefixB));
            var postfixB = new HarmonyMethod(typeof(HookBridgeChecks), nameof(PostfixB));
            ownerA.Patch(count, prefix: prefixA, postfix: postfixA);
            ownerB.Patch(amount, prefix: prefixB, postfix: postfixB);
            Expect(provider.Active == 1 && provider.MaximumActive == 1, "Aliases installed concurrent detours on one native entry");
            Expect(Harmony.GetPatchInfo(count).Owners.OrderBy(id => id).SequenceEqual(Harmony.GetPatchInfo(amount).Owners.OrderBy(id => id)),
                "Alias patch queries hide another owner's callbacks");
            Expect(Harmony.GetPatchInfo(count).Prefixes.Count == 2 && Harmony.GetPatchInfo(count).Postfixes.Count == 2,
                "Sharing a patcher lost an alias's callbacks");
            Expect(Il2Cpp.GameItem.SetUnitCount(7) == 7 && Il2Cpp.GameItem.SetAmount(9) == 9,
                "Alias invocation did not reach the original method");
            Expect(provider.OriginalCalls == 2 && _prefixA == 2 && _postfixA == 2 && _prefixB == 2 && _postfixB == 2,
                "Repeated patching duplicated a callback or skipped the original");

            ownerA.Unpatch(amount, HarmonyPatchType.All, ownerA.Id);
            Expect(Harmony.GetPatchInfo(count).Owners.SequenceEqual([ownerB.Id]), "Owner-scoped alias removal affected another mod");
            Expect(Il2Cpp.GameItem.SetUnitCount(11) == 11 && provider.OriginalCalls == 3 && _prefixA == 2 && _prefixB == 3,
                "Rebuilding after owner removal broke the remaining callback or original");
            ownerA.Patch(count, prefix: prefixA, postfix: postfixA);
            Expect(Il2Cpp.GameItem.SetAmount(13) == 13 && provider.OriginalCalls == 4 && _prefixA == 3 && _prefixB == 4,
                "Reinstalling an owner broke the original bridge");
            ownerA.UnpatchSelf();
            Expect(Il2Cpp.GameItem.SetAmount(17) == 17 && _prefixA == 3 && _prefixB == 5,
                "UnpatchSelf crossed owner boundaries");
            ownerB.Unpatch(count, HarmonyPatchType.All, ownerB.Id);
            Expect(Harmony.GetPatchInfo(amount).Owners.Count == 0 && Il2Cpp.GameItem.SetUnitCount(19) == 19,
                "Zero-patch rebuild still recurses or skips the original");
            Expect(provider.OriginalCalls == 6 && provider.MaximumDepth == 2 && provider.MaximumActive == 1,
                "Bridge grew recursively or duplicated the native entry");

            // Two Forge-style observers on one name, plus an external observer on
            // the other name. Legitimate nested quantity writes must still run.
            _prefixA = _postfixA = _prefixB = _postfixB = 0;
            ownerA.Patch(count, prefix: prefixA, postfix: postfixA);
            ownerB.Patch(count, prefix: prefixB, postfix: postfixB);
            ownerC.Patch(amount, prefix: new HarmonyMethod(typeof(HookBridgeChecks), nameof(PrefixC)),
                postfix: new HarmonyMethod(typeof(HookBridgeChecks), nameof(PostfixC)));
            Expect(Harmony.GetPatchInfo(count).Owners.Count == 3 && provider.Active == 1,
                "Three owners on both aliases installed independent native hooks");
            Expect(Il2Cpp.GameItem.SetUnitCount(23) == 23 && _nestedResult == 29 && provider.OriginalCalls == 8,
                "A legitimate nested quantity mutation was skipped or recursed indefinitely");
            Expect(_prefixA == 2 && _postfixA == 2 && _prefixB == 2 && _postfixB == 2 && _prefixC == 2 && _postfixC == 2,
                "Nested writes did not notify every remaining observer exactly once");
            Expect(provider.MaximumDepth == 3 && provider.MaximumActive == 1, "Nested writes created an unbounded bridge");
            ownerC.UnpatchSelf();
            Expect(Il2Cpp.GameItem.SetAmount(31) == 31 && provider.OriginalCalls == 9 && _prefixA == 3 && _prefixB == 3 && _prefixC == 2,
                "Removing the external observer affected Forge observers");
        }
        finally
        {
            ownerA.UnpatchSelf();
            ownerB.UnpatchSelf();
            ownerC.UnpatchSelf();
            PatchManager.ResolvePatcher -= Resolve;
            // Patcher metadata is retained by Harmony for this process lifetime.
        }
        Console.WriteLine($"IL2CPP rebuild and shared-entry bridge: {checks} assertions passed; native calls={provider.OriginalCalls}; max depth={provider.MaximumDepth}; max active detours={provider.MaximumActive}.");
        return checks;
    }

    private static void PrefixA() => _prefixA++;
    private static void PostfixA() => _postfixA++;
    private static void PrefixB() => _prefixB++;
    private static void PostfixB() => _postfixB++;
    private static void PrefixC(int __0)
    {
        _prefixC++;
        if (__0 == 23) _nestedResult = Il2Cpp.GameItem.SetAmount(29);
    }
    private static void PostfixC() => _postfixC++;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int FixtureNativeCall(int value, IntPtr metadata);

// Resolves synthetic entries without patching executable memory or loading a game.
internal sealed class RecordingDetourProvider : IDetourProvider
{
    private readonly Dictionary<IntPtr, RecordingDetour> _active = [];
    private readonly FixtureNativeCall _original;
    internal int OriginalCalls, MaximumActive, MaximumDepth;
    internal int Active => _active.Count;
    internal IntPtr Original { get; }
    internal RecordingDetourProvider()
    {
        _original = (value, _) => { OriginalCalls++; return value; };
        Original = Marshal.GetFunctionPointerForDelegate(_original);
    }
    public IDetour Create<TDelegate>(IntPtr original, TDelegate target) where TDelegate : Delegate => new RecordingDetour(this, original, target);
    internal IntPtr Resolve(IntPtr entry) => _active.TryGetValue(entry, out var detour) ? detour.Detour : entry;

    private sealed class RecordingDetour : IDetour
    {
        private readonly RecordingDetourProvider _provider;
        private readonly Delegate _callback;
        private readonly FixtureNativeCall _trampoline;
        public IntPtr Target { get; }
        public IntPtr Detour { get; }
        public IntPtr OriginalTrampoline { get; }
        internal unsafe RecordingDetour(RecordingDetourProvider provider, IntPtr target, Delegate callback)
        {
            _provider = provider; Target = target; _callback = callback;
            Detour = Marshal.GetFunctionPointerForDelegate(callback);
            _trampoline = (value, metadata) => ((delegate* unmanaged[Cdecl]<int, IntPtr, int>)target)(value, metadata);
            OriginalTrampoline = Marshal.GetFunctionPointerForDelegate(_trampoline);
        }
        public void Apply()
        {
            if (_provider._active.TryGetValue(Target, out var prior) && prior != this)
                throw new InvalidOperationException("Two native patchers attempted to detour the same address");
            _provider._active[Target] = this;
            _provider.MaximumActive = Math.Max(_provider.MaximumActive, _provider.Active);
        }
        public void Dispose() => _provider._active.Remove(Target);
        public T GenerateTrampoline<T>() where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(OriginalTrampoline);
    }
}

internal static class BridgeFixture
{
    internal static RecordingDetourProvider Provider = null!;
    [ThreadStatic] private static int _depth;
    internal static unsafe int Invoke(IntPtr metadata, int value)
    {
        if (++_depth > 12) { _depth--; throw new InvalidOperationException("Bounded bridge recursion detected"); }
        Provider.MaximumDepth = Math.Max(Provider.MaximumDepth, _depth);
        try
        {
            var entry = UnityVersionHandler.Wrap((Il2CppMethodInfo*)metadata).MethodPointer;
            return ((delegate* unmanaged[Cdecl]<int, IntPtr, int>)Provider.Resolve(entry))(value, metadata);
        }
        finally { _depth--; }
    }
}

namespace Il2Cpp
{
    internal static class GameItem
    {
        internal static IntPtr NativeMethodInfoPtr_SetUnitCount, NativeMethodInfoPtr_SetAmount;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int SetUnitCount(int value) => BridgeFixture.Invoke(NativeMethodInfoPtr_SetUnitCount, value);
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int SetAmount(int value) => BridgeFixture.Invoke(NativeMethodInfoPtr_SetAmount, value);
    }
}
