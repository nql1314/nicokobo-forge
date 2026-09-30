using HarmonyLib;

namespace Nicokobo.Forge.Diagnostics;

internal static class LifecycleProbe
{
    private static Action<string>? _log;
    private static bool _enabled;

    internal static bool Install(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _enabled = false;
        _log = log;
        harmony = new HarmonyLib.Harmony("nicokobo.forge.lifecycle_diagnostics");
        try
        {
            Patch(harmony, typeof(Il2Cpp.MiscItemDirectory), "InitDirectory",
                nameof(ItemDirectoryPostfix), postfix: true);
            Patch(harmony, typeof(Il2Cpp.PerkUIController), "OpenUI",
                nameof(PerkUiPostfix), postfix: true);
            Patch(harmony, typeof(Il2Cpp.PlayerStore), "InitialSave",
                nameof(InitialSavePrefix), postfix: false);
            Patch(harmony, typeof(Il2Cpp.ModHook), "FireOnGameLoadedLate",
                nameof(GameLoadedPostfix), postfix: true);
            _enabled = true;
            log("[NicokoboForge/P0] lifecycleHooks=installed; mode=read-only");
            return true;
        }
        catch (Exception ex)
        {
            try { harmony.UnpatchSelf(); } catch { }
            log($"[ERROR] [NicokoboForge/P0] lifecycleHooks=disabled; reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type target, string targetName,
        string probeName, bool postfix)
    {
        var original = AccessTools.Method(target, targetName, Type.EmptyTypes)
            ?? throw new MissingMethodException(target.FullName, targetName);
        var probe = AccessTools.Method(typeof(LifecycleProbe), probeName)
            ?? throw new MissingMethodException(nameof(LifecycleProbe), probeName);
        if (postfix) harmony.Patch(original, postfix: new HarmonyMethod(probe));
        else harmony.Patch(original, prefix: new HarmonyMethod(probe));
    }

    private static void ItemDirectoryPostfix()
    {
        if (_enabled) Emit("MiscItemDirectory.InitDirectory completed");
    }

    private static void PerkUiPostfix()
    {
        if (_enabled) Emit("PerkUIController.OpenUI completed");
    }

    private static void InitialSavePrefix(Il2Cpp.PlayerStore __instance)
    {
        if (!_enabled) return;
        try
        {
            Emit($"PlayerStore.InitialSave entered; slot={__instance.saveSlotId}; run={__instance.runID}; startType={(int)__instance.startType}");
        }
        catch (Exception ex)
        {
            Emit($"PlayerStore.InitialSave entered; fieldsUnavailable={ex.GetType().Name}");
        }
    }

    private static void GameLoadedPostfix()
    {
        if (_enabled) Emit("ModHook.FireOnGameLoadedLate completed");
    }

    private static void Emit(string message)
    {
        try { _log?.Invoke($"[NicokoboForge/P0] utc={DateTime.UtcNow:O}; {message}"); }
        catch { /* Logging must not change game control flow. */ }
    }
}
