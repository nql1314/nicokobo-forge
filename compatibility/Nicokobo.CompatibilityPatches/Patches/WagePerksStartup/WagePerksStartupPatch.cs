using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nicokobo.CompatibilityPatches.Patches.WagePerksStartup;

internal sealed class WagePerksStartupPatch
{
    private const string SupportedVersion = "1.3.4";
    private static WagePerksStartupPatch? _instance;
    private readonly Plugin _host;
    private MelonLogger.Instance Logger => _host.LoggerInstance;
    private readonly DeferredStartupPatches _gate = new();
    private MethodInfo? _tryPatch;
    private PropertyInfo? _okCount;
    private bool _armed, _failureLogged;

    internal WagePerksStartupPatch(Plugin host) => _host = host;

    internal void Initialize()
    {
        var target = MelonMod.RegisteredMelons.FirstOrDefault(mod =>
            mod.GetType().FullName == "WagePerks.Core" &&
            mod.MelonAssembly.Assembly.GetName().Name == "WagePerks");
        if (target == null)
        {
            Logger.Msg("[WagePerksStartup] Wage's Perks 未安装，此项补丁停用。");
            return;
        }
        if (target.Info.Version != SupportedVersion || target.Priority <= _host.Priority)
        {
            Logger.Warning($"[WagePerksStartup] 此项补丁停用：仅支持 Wage's Perks {SupportedVersion}，且必须先于目标初始化；当前版本={target.Info.Version}，priority={target.Priority}。");
            return;
        }
        try
        {
            var patcher = target.MelonAssembly.Assembly.GetType("WagePerks.ManualPatcher", true)!;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            _tryPatch = patcher.GetMethod("TryPatch", flags, null,
                new[] { typeof(Type), typeof(string), typeof(string), typeof(string),
                    typeof(Type[]), typeof(Type), typeof(int?), typeof(string) }, null);
            _okCount = patcher.GetProperty("PatchOkCount", flags);
            if (_tryPatch == null || _tryPatch.ReturnType != typeof(void) ||
                _okCount?.PropertyType != typeof(int) || _okCount.GetMethod == null)
                throw new MissingMethodException("WagePerks.ManualPatcher 的补丁入口或成功计数与已验证版本不一致");
            _instance = this;
            _host.HarmonyInstance.Patch(_tryPatch,
                prefix: new HarmonyMethod(typeof(WagePerksStartupPatch), nameof(BeforeTryPatch)) { priority = HarmonyLib.Priority.First });
            _armed = true;
            Logger.Msg($"[WagePerksStartup] 已拦截 Wage's Perks {SupportedVersion} 的四类本地化相关原生挂载，等待游戏本地化初始化完成。");
        }
        catch (Exception ex)
        {
            _instance = null;
            _gate.Disable();
            Logger.Error($"[WagePerksStartup] 此项补丁安装失败：{ex}");
        }
    }

    private static bool BeforeTryPatch(object[] __args)
    {
        var instance = _instance;
        if (instance == null || !DeferredStartupPatches.RequiresLocalization((__args[0] as Type)?.FullName))
            return true;
        // Preserve the target Mod's arguments without resolving native metadata.
        var arguments = (object[])__args.Clone();
        if (arguments[4] is Type[] parameterTypes) arguments[4] = (Type[])parameterTypes.Clone();
        bool runNow = instance._gate.Intercept(() => instance.InstallDeferred(arguments));
        if (!runNow && !instance._gate.Failed)
            instance.Logger.Msg($"[WagePerksStartup] 已暂存 {((Type)arguments[0]).Name}.{arguments[1]}。");
        return runNow;
    }

    private void InstallDeferred(object[] arguments)
    {
        int before = (int)_okCount!.GetValue(null)!;
        _tryPatch!.Invoke(null, arguments);
        int after = (int)_okCount.GetValue(null)!;
        if (after != before + 1)
            throw new InvalidOperationException($"Wage's Perks 未成功安装 {((Type)arguments[0]).Name}.{arguments[1]}（成功计数 {before} → {after}）");
        Logger.Msg($"[WagePerksStartup] 延后挂载成功：{((Type)arguments[0]).Name}.{arguments[1]}。");
    }

    internal void Update()
    {
        if (!_armed || _gate.Failed || _gate.PendingCount == 0) return;
        try
        {
            var state = ReadLocalizationState();
            _gate.Observe(state);
            if (state == LocalizationState.Failed)
                LogFailure("游戏本地化初始化失败，已取消延后挂载。");
        }
        catch (Exception ex)
        {
            _gate.Disable();
            LogFailure($"延后挂载停用，不再重试：{ex}");
        }
    }

    private static LocalizationState ReadLocalizationState()
    {
        if (!LocalizationSettings.HasSettings) return LocalizationState.Pending;
        // Read the existing operation. InitializationOperation and synchronous
        // WaitForCompletion would themselves start or block resource loading.
        var operation = LocalizationSettings.Instance.m_InitializingOperationHandle;
        if (operation == null || !operation.IsValid() || !operation.IsDone)
            return LocalizationState.Pending;
        return operation.Status == AsyncOperationStatus.Succeeded
            ? LocalizationState.Succeeded : LocalizationState.Failed;
    }

    private void LogFailure(string message)
    {
        if (_failureLogged) return;
        _failureLogged = true;
        Logger.Error("[WagePerksStartup] " + message);
    }

    internal void Deinitialize()
    {
        _armed = false;
        _gate.Disable();
        if (ReferenceEquals(_instance, this)) _instance = null;
    }
}
