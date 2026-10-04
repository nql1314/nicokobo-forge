using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

// Observe the native menu opening once. The shared list includes native and
// content-mod cards without taking ownership of their selection callbacks.
internal static class ForgeStartSelection
{
    private const string HookId = "nicokobo.forge.start_selection";
    private static StartSelectionList? _list;
    private static Action<string>? _log;
    internal static bool Installed { get; private set; }

    internal static void Install(bool knownBuild, Action<string> log)
    {
        _log = log;
        Installed = knownBuild && NativeHookSet.Install(HookId,
            [new(typeof(MainMenuUIController), nameof(MainMenuUIController.OnNewGameClick),
                [], typeof(void), typeof(ForgeStartSelection), Postfix: nameof(MenuOpened))], log);
    }

    private static void MenuOpened(MainMenuUIController __instance)
    {
        if (!Installed || __instance == null) return;
        try
        {
            var side = __instance.tabGeneralist?.transform.parent;
            if (side == null) return;
            if (_list == null || !_list.IsAlive || _list.Content != side)
                _list = new StartSelectionList(side);
            _list.MenuOpened();
        }
        catch (Exception ex) { Disable(ex); }
    }

    internal static void Update()
    {
        if (!Installed || _list == null) return;
        try
        {
            if (!_list.IsAlive) { _list = null; return; }
            _list.Update();
        }
        catch (Exception ex) { Disable(ex); }
    }

    private static void Disable(Exception ex)
    {
        Installed = false;
        NativeHookSet.Remove(HookId, _log);
        _log?.Invoke($"[ERROR] [NicokoboForge/StartSelection] disabled; {ex.GetType().Name}: {ex.Message}");
    }
}
