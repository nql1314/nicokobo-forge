using Nicokobo.Forge.Diagnostics;
using Nicokobo.Forge.Logging;

namespace Nicokobo.Forge;

// Installs independent adapters and publishes their capability gates once.
internal static class ForgeBootstrap
{
    internal static void Initialize(HarmonyLib.Harmony harmony, ModLogger log, string? gameRoot)
    {
        ForgeApi.SetLogger(log.Callback);
        ForgeNativeApi.SetLogger(log.Callback);
        ForgeNativeEffectApi.SetLogger(log.Callback);
        ForgeStartApi.SetLogger(log.Callback);
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
            LifecycleProbe.Install(harmony, log.Callback);
        if (log.IsDebugEnabled && report.CanInstallReadOnlyProbes && !installed)
            log.Warn("[NicokoboForge/P0] Read-only probes could not be installed.");

        var nativeNodeInstalled = report.KnownBuild &&
            report.MiscDirectorySignatureMatch && ForgeNativeApi.Install(harmony,
            report.ModuleDirectorySignaturesMatch,
            report.AmenityDirectorySignatureMatch,
            report.NightShopSignaturesMatch);
        ForgeNativeItemPresentation.Install(harmony,
            report.KnownBuild && nativeNodeInstalled,
            log.Callback);
        ForgeNativeEffectApi.Configure(harmony,
            report.KnownBuild && report.MiscDirectorySignatureMatch &&
                report.RandomEffectSignatureMatch && report.NativeEffectSignaturesMatch,
            report.ModuleDirectorySignaturesMatch);
        var nativeEffectInstalled = ForgeNativeEffectApi.HooksInstalled;
        var machineInstalled = ForgeMachineApi.Install(report.KnownBuild,
            log.Callback);
        var inventoryDragProbeInstalled = InventoryDragFaultProbe.Install(
            report.KnownBuild, log.Callback);
        ForgeCapabilities.Publish(new(report.KnownBuild, nativeNodeInstalled,
            ForgeNativeApi.ModuleHookInstalled,
            ForgeNativeApi.AmenityHookInstalled,
            ForgeNativeApi.NightShopHookInstalled,
            report.KnownBuild && report.RunDataSignaturesMatch,
            report.KnownBuild && report.InventoryReadSignaturesMatch,
            report.KnownBuild && report.InventoryPreviewSignaturesMatch,
            nativeEffectInstalled, false, machineInstalled));
        log.Info($"[NicokoboForge/P0] readOnlyProbesInstalled={installed}; " +
            $"nativeNodeHookInstalled={nativeNodeInstalled}; stagedNodes={ForgeNativeApi.StagedCount}; " +
            $"moduleHookInstalled={ForgeNativeApi.ModuleHookInstalled}; " +
            $"amenityHookInstalled={ForgeNativeApi.AmenityHookInstalled}; " +
            $"nightShopHookInstalled={ForgeNativeApi.NightShopHookInstalled}; " +
            $"nativeEffectHookInstalled={nativeEffectInstalled}; " +
            $"machineHooksInstalled={machineInstalled}; " +
            $"inventoryDragProbeInstalled={inventoryDragProbeInstalled}; " +
            $"stagedEffects={ForgeNativeEffectApi.StagedCount}; " +
            $"declarations={ForgeApi.Snapshot().Count}");
    }
}
