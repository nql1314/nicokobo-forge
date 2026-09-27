using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

[assembly: MelonInfo(typeof(Nicokobo.Forge.EffectRegistrationProbe.Plugin),
    "Nicokobo Forge Effect Registration Probe", "0.1.0", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge.EffectRegistrationProbe;

public sealed class Plugin : MelonMod
{
    private const string OwnerId = "nicokobo.forge.effect_probe";
    private const string EffectId = "nicokobo.forge.effect_probe.effect.ping";
    private const string NativeNodeEffectId = "NODE_EFFECT_SUPPORT";
    private static Il2CppSystem.Action<GameItem, GameInventory>? _anyAction;
    private static Action<string>? _log;
    private static int _callbackCount;
    private DateTime _nextCheckUtc;
    private DateTime _stagedSinceUtc;
    private bool _reportedTerminal;
    private bool _reportedWaiting;

    public override void OnInitializeMelon()
    {
        _log = message => LoggerInstance.Msg(message);
        _stagedSinceUtc = DateTime.UtcNow;
        var result = ForgeNativeEffectApi.RegisterEffect(
            OwnerId, EffectId, CreateEffect, randomEligible: false);
        LoggerInstance.Msg($"[EffectProbe] submit={result.Status}; reason={result.Reason}; " +
            "randomEligible=false");
        if (result.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent))
            _reportedTerminal = true;
    }

    public override void OnUpdate()
    {
        if (_reportedTerminal || DateTime.UtcNow < _nextCheckUtc) return;
        _nextCheckUtc = DateTime.UtcNow.AddSeconds(1);
        var application = ForgeNativeEffectApi.Snapshot()
            .FirstOrDefault(item => item.OwnerId == OwnerId && item.ContentId == EffectId);
        if (application?.Status == NativeApplicationStatus.Staged)
        {
            if (!_reportedWaiting && DateTime.UtcNow - _stagedSinceUtc > TimeSpan.FromSeconds(30))
            {
                _reportedWaiting = true;
                LoggerInstance.Warning("[EffectProbe] status=Staged after 30s; " +
                    "enter the main menu to initialize the native directories");
            }
            return;
        }
        if (application == null)
        {
            _reportedTerminal = true;
            LoggerInstance.Warning("[EffectProbe] registration outcome missing");
            return;
        }
        _reportedTerminal = true;
        if (application.Status != NativeApplicationStatus.Applied)
        {
            LoggerInstance.Warning($"[EffectProbe] status={application.Status}; " +
                $"reason={application.Reason}");
            return;
        }
        try
        {
            var registry = ModuleEffectHelper.moduleEffects;
            bool present = registry != null &&
                registry.TryGetValue(EffectId, out var effect) &&
                effect != null && effect.Pointer != IntPtr.Zero &&
                effect.identifier == EffectId;
            LoggerInstance.Msg($"[EffectProbe] status=Applied; " +
                $"nativeRegistryPresent={present}; " +
                $"capability={ForgeCapabilities.Current.NativeEffectRegistration}; " +
                $"callbackCount={System.Threading.Volatile.Read(ref _callbackCount)}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"[EffectProbe] native registry check failed: " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ModuleEffectHelper.ModuleEffect CreateEffect()
    {
        var registry = ModuleEffectHelper.moduleEffects;
        if (registry == null ||
            !registry.TryGetValue(NativeNodeEffectId, out var native) || native == null)
            throw new InvalidOperationException("Native support-node effect is unavailable");
        _anyAction ??= DelegateSupport.ConvertDelegate<
            Il2CppSystem.Action<GameItem, GameInventory>>(
            new Action<GameItem, GameInventory>(OnAnyAction));
        if (_anyAction == null)
            throw new InvalidOperationException("Effect callback conversion failed");
        _log?.Invoke("[EffectProbe] factory=called; nativeTemplate=NODE_EFFECT_SUPPORT");
        return new ModuleEffectHelper.ModuleEffect
        {
            identifier = EffectId,
            effectType = native.effectType,
            displayName = "Nicokobo Forge Effect Probe",
            description = "Diagnostic effect; logs callback invocation without changing stats.",
            onCreateAction = null,
            onAnyAction = _anyAction,
            onUsedAction = null,
            incompatibleWithEffects = native.incompatibleWithEffects
        };
    }

    private static void OnAnyAction(GameItem item, GameInventory inventory)
    {
        int count = System.Threading.Interlocked.Increment(ref _callbackCount);
        if (count <= 8)
            _log?.Invoke($"[EffectProbe] callback=onAny; count={count}; " +
                $"item={item?.identifier ?? "null"}; inventoryPresent={inventory != null}");
    }
}
