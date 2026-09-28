using MelonLoader;
using MelonLoader.Utils;
using Nicokobo.Forge.Diagnostics;
using Nicokobo.Forge.Logging;

[assembly: MelonInfo(typeof(Nicokobo.Forge.Plugin), "Nicokobo Forge", "0.1.0", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge;

public sealed class Plugin : MelonMod
{
    private ModLogger? _log;

    public override void OnInitializeMelon()
    {
        var log = _log = new ModLogger(typeof(Plugin), "NicokoboForge", "Nicokobo Forge",
            message => LoggerInstance.Msg(message),
            message => LoggerInstance.Warning(message),
            message => LoggerInstance.Error(message));
        ForgeApi.SetLogger(log.Callback);
        ForgeNativeApi.SetLogger(log.Callback);
        ForgeNativeEffectApi.SetLogger(log.Callback);
        ForgeStartApi.SetLogger(log.Callback);
        var gameRoot = Path.GetDirectoryName(MelonEnvironment.ModsDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var report = BuildProbe.Run(gameRoot);
        foreach (var line in report.Lines)
            log.Debug(line);
        if (!report.KnownBuild || !report.SignaturesMatch)
            log.Warn("[NicokoboForge] Build or required signatures did not match; affected native hooks are disabled.");
        ForgeWorkshopApi.Configure(report.KnownBuild,
            log.Callback);
        ForgeRunDataApi.SetEnabled(report.KnownBuild && report.RunDataSignaturesMatch);
        ForgeNativeInventoryApi.SetEnabled(report.KnownBuild &&
            report.InventoryReadSignaturesMatch, report.KnownBuild &&
            report.InventoryPreviewSignaturesMatch);

        var installed = log.IsDebugEnabled && report.CanInstallReadOnlyProbes &&
            LifecycleProbe.Install(HarmonyInstance, log.Callback);
        if (log.IsDebugEnabled && report.CanInstallReadOnlyProbes && !installed)
            log.Warn("[NicokoboForge/P0] Read-only probes could not be installed.");

        var nativeNodeInstalled = report.KnownBuild &&
            report.MiscDirectorySignatureMatch && ForgeNativeApi.Install(HarmonyInstance,
            report.ModuleDirectorySignaturesMatch,
            report.AmenityDirectorySignatureMatch,
            report.NightShopSignaturesMatch);
        ForgeNativeItemPresentation.Install(HarmonyInstance,
            report.KnownBuild && nativeNodeInstalled,
            log.Callback);
        ForgeTrashCleanup.Install(HarmonyInstance,
            report.KnownBuild && nativeNodeInstalled,
            log.Callback);
        ForgeNativeEffectApi.Configure(HarmonyInstance,
            report.KnownBuild && report.MiscDirectorySignatureMatch &&
                report.RandomEffectSignatureMatch && report.NativeEffectSignaturesMatch,
            report.ModuleDirectorySignaturesMatch);
        var nativeEffectInstalled = ForgeNativeEffectApi.HooksInstalled;
        ForgeCapabilities.Publish(new(report.KnownBuild, nativeNodeInstalled,
            ForgeNativeApi.ModuleHookInstalled,
            ForgeNativeApi.AmenityHookInstalled,
            ForgeNativeApi.NightShopHookInstalled,
            report.KnownBuild && report.RunDataSignaturesMatch,
            report.KnownBuild && report.InventoryReadSignaturesMatch,
            report.KnownBuild && report.InventoryPreviewSignaturesMatch,
            nativeEffectInstalled, false));
        log.Info($"[NicokoboForge/P0] readOnlyProbesInstalled={installed}; " +
            $"nativeNodeHookInstalled={nativeNodeInstalled}; stagedNodes={ForgeNativeApi.StagedCount}; " +
            $"moduleHookInstalled={ForgeNativeApi.ModuleHookInstalled}; " +
            $"amenityHookInstalled={ForgeNativeApi.AmenityHookInstalled}; " +
            $"nightShopHookInstalled={ForgeNativeApi.NightShopHookInstalled}; " +
            $"nativeEffectHookInstalled={nativeEffectInstalled}; " +
            $"stagedEffects={ForgeNativeEffectApi.StagedCount}; " +
            $"declarations={ForgeApi.Snapshot().Count}");
    }

    public override void OnUpdate()
    {
        ForgeNativeApi.Update();
        ForgeWorkshopApi.Update();
    }

    public override void OnGUI() => ForgeWorkshopApi.Draw();
}
