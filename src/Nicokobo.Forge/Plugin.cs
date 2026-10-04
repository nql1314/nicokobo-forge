using MelonLoader;
using MelonLoader.Utils;
using Nicokobo.Forge.Logging;

[assembly: MelonInfo(typeof(Nicokobo.Forge.Plugin), "Nicokobo Forge", "0.6.16", "Nicokobo")]
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
        NativeNpcStockAdapter.Update();
        Workshop.NativeWorkshop.Update();
        ForgeWorkshopApi.Update();
    }

    public override void OnGUI() => ForgeWorkshopApi.Draw();
}
