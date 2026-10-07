using MelonLoader;
using MelonLoader.Utils;
using Nicokobo.Forge.Logging;

[assembly: MelonInfo(typeof(Nicokobo.Forge.Plugin), "Nicokobo Forge", "0.6.32", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge;

public sealed class Plugin : MelonMod
{
    public override void OnInitializeMelon()
    {
        var log = new ModLogger(typeof(Plugin), "NicokoboForge", "Nicokobo Forge",
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message),
            message => LoggerInstance.Error(message));
        var gameRoot = Path.GetDirectoryName(MelonEnvironment.ModsDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        ForgeBootstrap.Initialize(HarmonyInstance, log, gameRoot);
    }

    public override void OnUpdate()
    {
        ForgeStartSelection.Update();
        ForgeModuleApi.UpdateActions();
        NativeNpcStockAdapter.Update();
        Workshop.NativeWorkshop.Update();
        ForgeWorkshopApi.Update();
        RecipeGuideBrowser.Update();
    }

    public override void OnLateUpdate() => RecipeGuideBrowser.UpdateCursor();
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) => RecipeGuideBrowser.ClearScene();
    public override void OnDeinitializeMelon()
    {
        RecipeGuideBrowser.Uninstall();
        MissingItemSaveRuntime.Uninstall();
    }

    public override void OnGUI() => ForgeWorkshopApi.Draw();
}
