using System.Runtime.InteropServices;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace Nicokobo.Forge.Runtime;

// Only the synchronous main-menu preview operation is protected. Actual save loading
// must keep its own failure semantics; suppressing errors there could damage a run.
internal static class SaveListRuntime
{
    private const string HookId = "nicokobo.forge.save_list";
    private static bool _installed;
    private static Action<string>? _log;
    [ThreadStatic] private static OpenScope? _current;

    private sealed class OpenScope(OpenScope? previous)
    {
        internal readonly OpenScope? Previous = previous;
        internal bool Finished;
    }

    internal static void Install(bool knownBuild, Action<string> log)
    {
        if (_installed || !knownBuild) return;
        try
        {
            NativeHookSet.Require(typeof(MainMenuUIController), "get_saveUIManager", [], typeof(SaveUIManager));
            NativeHookSet.Require(typeof(MainMenuUIController), "get_confirmPanelUIManager", [], typeof(ConfirmPanelUIManager));
            NativeHookSet.Require(typeof(SaveUIManager), nameof(SaveUIManager.CloseUI), [], typeof(void));
            NativeHookSet.Require(typeof(ConfirmPanelUIManager), nameof(ConfirmPanelUIManager.OpenPanel),
                [typeof(string), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action), typeof(bool)], typeof(void));
            _log = log;
            _installed = NativeHookSet.Install(HookId,
            [
                new(typeof(MainMenuUIController), nameof(MainMenuUIController.OnLoadGameClick), [], typeof(void),
                    typeof(SaveListRuntime), Prefix: nameof(BeforeOpen), Finalizer: nameof(FinishOpen)),
                new(typeof(IconProvider), nameof(IconProvider.GetIcon), [typeof(NewGameData.StartType)], typeof(Sprite),
                    typeof(SaveListRuntime), Finalizer: nameof(FinishIcon))
            ], log);
            if (_installed) log("[INFO] [NicokoboForge/SaveList] preview exception guards installed");
        }
        catch (Exception ex)
        {
            log($"[WARN] [NicokoboForge/SaveList] preview exception guards disabled: {ex.Message}");
        }
    }

    internal static void Uninstall()
    {
        if (_installed) NativeHookSet.Remove(HookId, null);
        _installed = false;
        _log = null;
        _current = null;
    }

    [HarmonyPriority(Priority.First)]
    private static void BeforeOpen(out OpenScope __state)
    {
        __state = new OpenScope(_current);
        _current = __state;
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? FinishIcon(NewGameData.StartType __0, ref Sprite __result, Exception? __exception)
    {
        if (_current == null || __exception == null || !Recoverable(__exception)) return __exception;
        try
        {
            // Avoid calling the failing icon provider (or another patched getter) again.
            __result = MissingSpriteRuntime.Placeholder();
            Warn($"startType={(int)__0}; substituted preview icon", __exception);
            return null;
        }
        catch (Exception recovery) when (Recoverable(recovery))
        {
            Warn("preview icon replacement failed", recovery);
            return __exception;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? FinishOpen(MainMenuUIController __instance, OpenScope? __state, Exception? __exception)
    {
        if (__state == null || __state.Finished) return __exception;
        __state.Finished = true;
        _current = __state.Previous;
        if (__exception == null || !Recoverable(__exception)) return __exception;
        Warn("opening save list failed", __exception);
        try
        {
            if (__instance == null) return __exception;
            var saves = __instance.saveUIManager;
            var notice = __instance.confirmPanelUIManager;
            if (saves == null || notice == null) return __exception;
            saves.CloseUI();
            notice.OpenPanel(ForgePresentationApi.PreferEnglish()
                ? "Could not open the save list. A save preview or Mod encountered an error. Your saves have not been changed. See the MelonLoader log for details."
                : "无法打开存档列表，存档预览或 Mod 出现异常。存档未被修改，请查看 MelonLoader 日志排查。",
                null, null, true);
            return null;
        }
        catch (Exception recovery) when (Recoverable(recovery))
        {
            Warn("could not restore main menu after preview error", recovery);
            return __exception;
        }
    }

    private static bool Recoverable(Exception exception) => exception is not
        (OutOfMemoryException or StackOverflowException or AccessViolationException or SEHException)
        && exception.GetType().FullName != "System.ExecutionEngineException";

    private static void Warn(string context, Exception exception)
    {
        try { _log?.Invoke($"[WARN] [NicokoboForge/SaveList] {context}: {exception}"); }
        catch (Exception logging) when (Recoverable(logging)) { /* Logging must not break recovery. */ }
    }
}
