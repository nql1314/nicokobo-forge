using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;

namespace Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars;

internal sealed class PCExpansionScalarsPatch(Plugin host)
{
    private static PCExpansionScalarsPatch? _instance;
    private readonly List<(MethodInfo Target, MethodInfo Prefix)> _hooks = [];
    private bool _mappedLogged;

    internal void Initialize()
    {
        var target = MelonMod.RegisteredMelons.FirstOrDefault(mod => mod.MelonAssembly.Assembly.GetName().Name == "PCExpansion");
        if (target == null) { host.LoggerInstance.Msg("[PCExpansionScalars] 深空装机未安装，此项补丁停用。"); return; }
        if (target.Info.Version != "2.6" || target.Priority <= host.Priority)
        {
            host.LoggerInstance.Warning($"[PCExpansionScalars] 此项补丁停用：仅支持装机 2.6，且必须先于目标初始化；当前版本={target.Info.Version}，priority={target.Priority}。");
            return;
        }
        if (MelonMod.RegisteredMelons.Any(mod => mod.MelonAssembly.Assembly.GetName().Name == "PCExpansionLegacyFix"))
        {
            host.LoggerInstance.Warning("[PCExpansionScalars] 检测到 PCExpansionLegacyFix，请移出旧兼容包后使用本项；旧包会跳过回读校验并拦截 Forge 数量钩子。此项停用。");
            return;
        }
        try
        {
            var assembly = target.MelonAssembly.Assembly;
            var codec = assembly.GetType("PCExpansion.WorkroomItemCodec", true)!;
            var snapshot = codec.GetNestedType("Snapshot", BindingFlags.NonPublic)!;
            var node = codec.GetNestedType("Node", BindingFlags.NonPublic)!;
            var repair = assembly.GetType("PCExpansion.WorkroomMachineRepair", true)!;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var apply = codec.GetMethod("ApplyScalars", flags, null, [typeof(object), typeof(Dictionary<string, JsonElement>)], null);
            var equivalent = codec.GetMethod("Equivalent", flags, null, [snapshot, snapshot], null);
            var preserved = repair.GetMethod("VerifyPreserved", flags, null, [node, node], null);
            if (apply?.ReturnType != typeof(void) || equivalent?.ReturnType != typeof(bool) || preserved?.ReturnType != typeof(void))
                throw new MissingMethodException("装机字段恢复或完整回读入口与已验证版本不一致");
            _instance = this;
            Install(apply, nameof(BeforeApply));
            Install(equivalent, nameof(BeforeCompare));
            Install(preserved, nameof(BeforeCompare));
            host.LoggerInstance.Msg("[PCExpansionScalars] 已安装储物／维修字段别名兼容；保留原始数量、标签、特性、身份和完整回读校验。");
        }
        catch (Exception ex)
        {
            Deinitialize();
            host.LoggerInstance.Error("[PCExpansionScalars] 此项补丁安装失败：" + ex);
        }
    }

    private void Install(MethodInfo target, string prefix)
    {
        var method = typeof(PCExpansionScalarsPatch).GetMethod(prefix, BindingFlags.Static | BindingFlags.NonPublic)!;
        _hooks.Add((target, method));
        host.HarmonyInstance.Patch(target, prefix: new HarmonyMethod(method));
    }

    private static void BeforeApply(object __0, ref Dictionary<string, JsonElement> __1)
    {
        var fields = ScalarAliases.RestoreFields(__0.GetType(), __1);
        if (!fields.Keys.SequenceEqual(__1.Keys)) _instance?.Mapped();
        __1 = fields;
    }

    private static void BeforeCompare(ref object __0, ref object __1)
    {
        // Normalize detached DTO copies; the original validator still runs and
        // neither saved records nor live candidate objects are rewritten here.
        // HarmonyX does not propagate replacements of __args array entries.
        // Replace the actual arguments so the original validator sees the copies.
        __0 = ScalarAliases.ComparisonCopy(__0);
        __1 = ScalarAliases.ComparisonCopy(__1);
    }

    private void Mapped()
    {
        if (_mappedLogged) return;
        _mappedLogged = true;
        host.LoggerInstance.Msg("[PCExpansionScalars] 已映射旧字段名；未知字段或别名值冲突仍阻止恢复，原储物记录保留。");
    }

    internal void Deinitialize()
    {
        if (ReferenceEquals(_instance, this)) _instance = null;
        foreach (var (target, prefix) in _hooks) host.HarmonyInstance.Unpatch(target, prefix);
        _hooks.Clear();
    }
}

