using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

public sealed record NativeEffectRegistration(string EffectId,
    Func<ModuleEffectHelper.ModuleEffect> Factory, bool RandomEligible = false);

/// <summary>Build-specific registration of native module/node effects. Content
/// Mods supply the effect object and its callbacks; Nicokobo Forge owns ID claims,
/// registry timing and optional exclusion from the native random roll.</summary>
public static class ForgeNativeEffectApi
{
    private sealed record RemovedEffect(string Id,
        ModuleEffectHelper.ModuleEffect Effect);

    private static readonly object Gate = new();
    private static readonly NativeEffectCatalog Catalog = new();
    private static readonly Dictionary<string, IntPtr> Applied =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, NativeApplicationView> Outcomes =
        new(StringComparer.Ordinal);
    private static Action<string>? _log;
    private static HarmonyLib.Harmony? _harmony;
    private static bool _allowed;
    private static bool _allowModuleDirectory;
    private static bool _directoryHookInstalled;
    private static bool _enabled;
    private static bool _installAttempted;
    private static bool _randomInstallAttempted;

    public static SubmitResult RegisterEffect(string ownerId, string effectId,
        Func<ModuleEffectHelper.ModuleEffect> factory,
        bool randomEligible = false)
    {
        SubmitResult result;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.Submit(ownerId, effectId, randomEligible, factory);
            if (result.Status == SubmitStatus.Accepted)
                Outcomes[effectId] = new(ownerId, effectId, "Effect",
                    NativeApplicationStatus.Staged, result.Reason);
            log = _log;
        }
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(log, prefix + $"[NicokoboForge/Effect] owner={ownerId}; id={effectId}; " +
            $"randomEligible={randomEligible}; status={result.Status}; reason={result.Reason}");
        if (result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)
            TryInstall();
        return result;
    }

    /// <summary>Claim all effect IDs together, or leave the catalog unchanged.</summary>
    public static SubmitResult RegisterEffects(string ownerId,
        IReadOnlyList<NativeEffectRegistration> effects)
    {
        if (effects == null)
            return new(SubmitStatus.Invalid, "Effect batch is null");
        SubmitResult result;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.SubmitBatch(ownerId, effects.Select(effect =>
                new NativeEffectBatchEntry(effect.EffectId,
                    effect.RandomEligible, effect.Factory)).ToArray());
            if (result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)
                foreach (var effect in effects)
                    Outcomes.TryAdd(effect.EffectId, new(ownerId, effect.EffectId,
                        "Effect", NativeApplicationStatus.Staged, result.Reason));
            log = _log;
        }
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(log, prefix + $"[NicokoboForge/Effect] owner={ownerId}; batch={effects.Count}; " +
            $"status={result.Status}; reason={result.Reason}");
        if (result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)
            TryInstall();
        return result;
    }

    public static IReadOnlyList<NativeApplicationView> Snapshot()
    {
        lock (Gate)
            return Outcomes.Values.OrderBy(x => x.ContentId,
                StringComparer.Ordinal).ToArray();
    }

    internal static int StagedCount
    {
        get { lock (Gate) return Catalog.Count; }
    }

    internal static void SetLogger(Action<string> log)
    {
        NativeEffectDeclaration[] staged;
        lock (Gate)
        {
            _log = log;
            staged = Catalog.Snapshot().ToArray();
        }
        foreach (var effect in staged)
            SafeLog(log, $"[NicokoboForge/Effect] owner={effect.OwnerId}; " +
                $"id={effect.EffectId}; status=Staged; registry application pending");
    }

    internal static bool HooksInstalled => _enabled;

    internal static void Configure(HarmonyLib.Harmony harmony, bool allowed,
        bool allowModuleDirectory)
    {
        _harmony = harmony;
        _allowed = allowed;
        _allowModuleDirectory = allowModuleDirectory;
        if (allowed && StagedCount > 0) TryInstall();
    }

    private static bool TryInstall()
    {
        lock (Gate)
        {
            if (_directoryHookInstalled) return true;
            if (!_allowed || _harmony == null || _installAttempted) return false;
            _installAttempted = true;
        }
        try
        {
            var directory = AccessTools.Method(typeof(MiscItemDirectory),
                nameof(MiscItemDirectory.InitDirectory), Type.EmptyTypes)
                ?? throw new MissingMethodException(nameof(MiscItemDirectory),
                    nameof(MiscItemDirectory.InitDirectory));
            _harmony.Patch(directory, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(ForgeNativeEffectApi), nameof(DirectoryPostfix))));
            if (_allowModuleDirectory)
            {
                try
                {
                    var moduleDirectory = AccessTools.Method(typeof(ModuleDirectory),
                        nameof(ModuleDirectory.InitDirectory), Type.EmptyTypes)
                        ?? throw new MissingMethodException(nameof(ModuleDirectory),
                            nameof(ModuleDirectory.InitDirectory));
                    _harmony.Patch(moduleDirectory, postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(ForgeNativeEffectApi),
                            nameof(DirectoryPostfix))));
                }
                catch (Exception ex)
                {
                    SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] moduleDirectoryHook=disabled; " +
                        $"reason={ex.GetType().Name}: {ex.Message}");
                }
            }
            _directoryHookInstalled = true;
            SafeLog(_log, "[NicokoboForge/Effect] registryHook=installed; randomPoolHook=pending-directory; buildGated=true");
            return true;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] registryHook=disabled; reason={ex}");
            return false;
        }
    }

    private static void DirectoryPostfix()
    {
        if (!_directoryHookInstalled) return;
        try
        {
            if (ModuleEffectHelper.moduleEffects == null || !TryInstallRandomHook())
                return;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] registryReady=Failed; reason={ex}");
            return;
        }
        NativeEffectDeclaration[] staged;
        lock (Gate) staged = Catalog.Snapshot().ToArray();
        foreach (var declaration in staged)
            Apply(declaration);
    }

    private static bool TryInstallRandomHook()
    {
        lock (Gate)
        {
            if (_enabled) return true;
            if (_harmony == null || _randomInstallAttempted) return false;
            _randomInstallAttempted = true;
        }
        try
        {
            var randomRoll = AccessTools.Method(typeof(ModuleEffectHelper),
                nameof(ModuleEffectHelper.InitRandomEffect),
                [typeof(GameItem), typeof(int)])
                ?? throw new MissingMethodException(nameof(ModuleEffectHelper),
                    nameof(ModuleEffectHelper.InitRandomEffect));
            _harmony.Patch(randomRoll,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ForgeNativeEffectApi),
                    nameof(RandomPrefix))),
                finalizer: new HarmonyMethod(AccessTools.Method(typeof(ForgeNativeEffectApi),
                    nameof(RandomFinalizer))));
            _enabled = true;
            ForgeCapabilities.Publish(ForgeCapabilities.Current with
            { NativeEffectRegistration = true });
            SafeLog(_log, "[NicokoboForge/Effect] randomPoolHook=installed; registryReady=true");
            return true;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] randomPoolHook=disabled; reason={ex}");
            return false;
        }
    }

    private static void Apply(NativeEffectDeclaration declaration)
    {
        try
        {
            var registry = ModuleEffectHelper.moduleEffects
                ?? throw new InvalidOperationException("Native effect registry unavailable");
            if (registry.TryGetValue(declaration.EffectId, out var existing))
            {
                bool ownPrevious;
                lock (Gate)
                    ownPrevious = existing != null &&
                        Applied.TryGetValue(declaration.EffectId, out var pointer) &&
                        pointer == existing.Pointer;
                SafeLog(_log, $"{(ownPrevious ? "" : "[WARN] ")}[NicokoboForge/Effect] owner={declaration.OwnerId}; " +
                    $"id={declaration.EffectId}; status={(ownPrevious ? "AlreadyApplied" : "Conflict")}");
                SetOutcome(declaration, ownPrevious ? NativeApplicationStatus.Applied :
                    NativeApplicationStatus.Conflict,
                    ownPrevious ? "Native registry still contains owned ID" :
                        "Native effect registry already contains ID");
                return;
            }
            var factory = (Func<ModuleEffectHelper.ModuleEffect>)declaration.Factory;
            var effect = factory();
            if (effect == null || effect.Pointer == IntPtr.Zero ||
                effect.identifier != declaration.EffectId)
                throw new InvalidOperationException("Factory returned an invalid effect or ID");
            registry.Add(declaration.EffectId, effect);
            lock (Gate) Applied[declaration.EffectId] = effect.Pointer;
            SetOutcome(declaration, NativeApplicationStatus.Applied,
                "Native effect registry accepted factory");
            SafeLog(_log, $"[NicokoboForge/Effect] owner={declaration.OwnerId}; " +
                $"id={declaration.EffectId}; status=Applied; " +
                $"randomEligible={declaration.RandomEligible}");
        }
        catch (Exception ex)
        {
            SetOutcome(declaration, NativeApplicationStatus.Failed,
                $"{ex.GetType().Name}: {ex.Message}");
            SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] owner={declaration.OwnerId}; " +
                $"id={declaration.EffectId}; status=Failed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SetOutcome(NativeEffectDeclaration effect,
        NativeApplicationStatus status, string reason)
    {
        lock (Gate)
            Outcomes[effect.EffectId] = new(effect.OwnerId, effect.EffectId,
                "Effect", status, reason);
    }

    private static void RandomPrefix(out RemovedEffect[]? __state)
    {
        __state = null;
        if (!_enabled) return;
        var registry = ModuleEffectHelper.moduleEffects;
        if (registry == null) return;
        NativeEffectDeclaration[] excluded;
        lock (Gate)
            excluded = Catalog.Snapshot().Where(e => !e.RandomEligible).ToArray();
        var removed = new List<RemovedEffect>();
        try
        {
            foreach (var declaration in excluded)
            {
                if (!registry.TryGetValue(declaration.EffectId, out var effect) ||
                    effect == null) continue;
                bool owned;
                lock (Gate)
                    owned = Applied.TryGetValue(declaration.EffectId, out var pointer) &&
                        pointer == effect.Pointer;
                if (!owned) continue;
                if (registry.Remove(declaration.EffectId))
                    removed.Add(new(declaration.EffectId, effect));
            }
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] randomPoolExclude=Failed; " +
                $"reason={ex.GetType().Name}");
        }
        finally { __state = removed.ToArray(); }
    }

    private static Exception? RandomFinalizer(Exception? __exception,
        RemovedEffect[]? __state)
    {
        if (__state == null || __state.Length == 0) return __exception;
        var registry = ModuleEffectHelper.moduleEffects;
        if (registry == null)
        {
            SafeLog(_log, "[ERROR] [NicokoboForge/Effect] randomPoolRestore=Failed; registry unavailable");
            return __exception;
        }
        foreach (var removed in __state)
        {
            try
            {
                if (!registry.TryGetValue(removed.Id, out var existing))
                    registry.Add(removed.Id, removed.Effect);
                else if (existing == null || existing.Pointer != removed.Effect.Pointer)
                    SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] id={removed.Id}; " +
                        "randomPoolRestore=Conflict");
            }
            catch (Exception ex)
            {
                SafeLog(_log, $"[ERROR] [NicokoboForge/Effect] id={removed.Id}; " +
                    $"randomPoolRestore=Failed; reason={ex.GetType().Name}");
            }
        }
        return __exception;
    }

    private static void SafeLog(Action<string>? log, string message)
    {
        try { log?.Invoke(message); }
        catch { /* Logging must not affect native registration. */ }
    }
}
