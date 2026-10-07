using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace Nicokobo.CompatibilityPatches.Patches.WagePerksDestinyDice;

internal sealed class WagePerksDestinyDicePatch
{
    private const string SupportedVersion = "1.3.4";
    private const string DiceId = "destiny_dice";
    private static WagePerksDestinyDicePatch? _instance;
    private readonly Plugin _host;
    private MethodInfo? _directoryHook, _register, _has;
    private bool _armed, _registrationLogged;

    internal WagePerksDestinyDicePatch(Plugin host) => _host = host;

    internal void Initialize()
    {
        var target = MelonMod.RegisteredMelons.FirstOrDefault(mod =>
            mod.GetType().FullName == "WagePerks.Core" &&
            mod.MelonAssembly.Assembly.GetName().Name == "WagePerks");
        if (target == null)
        {
            _host.LoggerInstance.Msg("[WagePerksDestinyDice] Wage's Perks 未安装，此项补丁停用。");
            return;
        }
        if (target.Info.Version != SupportedVersion || target.Priority <= _host.Priority)
        {
            _host.LoggerInstance.Warning($"[WagePerksDestinyDice] 此项补丁停用：仅支持 Wage's Perks {SupportedVersion}，且必须先于目标初始化；当前版本={target.Info.Version}，priority={target.Priority}。");
            return;
        }
        try
        {
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public;
            var assembly = target.MelonAssembly.Assembly;
            _register = assembly.GetType("WagePerks.DestinyDice", true)!
                .GetMethod("RegisterToDirectory", staticFlags);
            var parameters = _register?.GetParameters();
            if (_register?.ReturnType != typeof(void) || parameters?.Length != 1 ||
                parameters[0].ParameterType.FullName != "Il2Cpp.ItemDirectory")
                throw new MissingMethodException("WagePerks.DestinyDice 的原物品注册入口与已验证版本不一致");
            var directoryType = parameters[0].ParameterType;
            _directoryHook = assembly.GetType("WagePerks.Patches", true)!
                .GetMethod("PostfixInitDirectory", staticFlags, null, [directoryType], null);
            _has = directoryType.GetMethod("Has", BindingFlags.Instance | BindingFlags.Public,
                null, [typeof(string)], null);
            if (_directoryHook?.ReturnType != typeof(void) || _has?.ReturnType != typeof(bool))
                throw new MissingMethodException("WagePerks 的目录初始化入口或原生 Has 签名不一致");

            // Supplement the Mod's existing managed callback. Its native directory
            // hooks already run at the right time; do not create or grant a new die.
            _instance = this;
            _host.HarmonyInstance.Patch(_directoryHook,
                postfix: new HarmonyMethod(typeof(WagePerksDestinyDicePatch), nameof(AfterInitDirectory)));
            _armed = true;
            _host.LoggerInstance.Msg("[WagePerksDestinyDice] 已补齐命运骰子的目录注册挂点，使用 WagePerks 原物品工厂。");
        }
        catch (Exception ex)
        {
            Deinitialize();
            _host.LoggerInstance.Error($"[WagePerksDestinyDice] 此项补丁安装失败：{ex}");
        }
    }

    private static void AfterInitDirectory(object[] __args, bool __runOriginal)
    {
        var instance = _instance;
        if (instance == null || !instance._armed || !__runOriginal) return;
        try
        {
            if (__args.Length != 1 || __args[0] == null)
                throw new InvalidOperationException("目录初始化回调未提供原物品目录");
            // RegisterToDirectory holds its IL2CPP delegate and skips IDs already
            // present. Native DecodeNodes then restores the saved identity/tags.
            instance._register!.Invoke(null, __args);
            if (!(bool)instance._has!.Invoke(__args[0], [DiceId])!)
                throw new InvalidOperationException("原注册函数返回后，目录仍缺少命运骰子");
            if (!instance._registrationLogged)
            {
                instance._registrationLogged = true;
                instance._host.LoggerInstance.Msg("[WagePerksDestinyDice] 命运骰子目录注册已确认。");
            }
        }
        catch (Exception ex)
        {
            instance._armed = false;
            instance._host.LoggerInstance.Error($"[WagePerksDestinyDice] 注册失败，此项补丁停用，不再重试：{ex}");
        }
    }

    internal void Deinitialize()
    {
        _armed = false;
        if (ReferenceEquals(_instance, this)) _instance = null;
        if (_directoryHook != null)
            _host.HarmonyInstance.Unpatch(_directoryHook,
                AccessTools.Method(typeof(WagePerksDestinyDicePatch), nameof(AfterInitDirectory)));
    }
}
