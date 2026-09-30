using Nicokobo.Forge.Diagnostics;
using Nicokobo.Forge.Logging;

namespace Nicokobo.Forge;

// Installs independent adapters and publishes their capability gates once.
internal static class ForgeBootstrap
{
    internal static void Initialize(HarmonyLib.Harmony harmony, ModLogger log, string? gameRoot)
    {
        ForgeApi.SetLogger(log.Callback);
        NativeItemRegistry.SetLogger(log.Callback);
        NativeEffectRegistry.SetLogger(log.Callback);
        ForgeStartApi.SetLogger(log.Callback);
        ForgeContentApi.Configure(log.Callback);
        var report = BuildProbe.Run(gameRoot);
        foreach (var line in report.Lines)
            log.Debug(line);
        if (!report.KnownBuild || !report.SignaturesMatch)
            log.Warn("[NicokoboForge] Build or required signatures did not match; affected native hooks are disabled.");
        ForgeWorkshopApi.Configure(report.KnownBuild,
            log.Callback);
        ForgeRunDataApi.SetEnabled(report.KnownBuild && report.RunDataSignaturesMatch);
        ForgeLifecycleApi.Configure(report.KnownBuild && report.RunDataSignaturesMatch, log.Callback);
        ForgeModuleApi.Configure(report.KnownBuild && report.ModuleDirectorySignaturesMatch, log.Callback);
        ForgePresentationApi.Configure(report.KnownBuild && report.ModuleDirectorySignaturesMatch, log.Callback);
        ForgeInventoryApi.SetEnabled(report.KnownBuild &&
            report.InventoryReadSignaturesMatch, report.KnownBuild &&
            report.InventoryPreviewSignaturesMatch);

        var installed = log.IsDebugEnabled && report.CanInstallReadOnlyProbes &&
            LifecycleProbe.Install(harmony, log.Callback);
        if (log.IsDebugEnabled && report.CanInstallReadOnlyProbes && !installed)
            log.Warn("[NicokoboForge/P0] Read-only probes could not be installed.");

        var nativeNodeInstalled = report.KnownBuild &&
            report.MiscDirectorySignatureMatch && NativeItemRegistry.Install(harmony,
            report.ModuleDirectorySignaturesMatch,
            report.AmenityDirectorySignatureMatch);
        NativeShopAdapter.Configure(report.KnownBuild && report.NightShopSignaturesMatch, log.Callback);
        ForgeNativeItemPresentation.Install(harmony,
            report.KnownBuild && nativeNodeInstalled,
            log.Callback);
        NativeEffectRegistry.Configure(harmony,
            report.KnownBuild && report.MiscDirectorySignatureMatch &&
                report.RandomEffectSignatureMatch && report.NativeEffectSignaturesMatch);
        var nativeEffectInstalled = NativeEffectRegistry.HooksInstalled;
        var machineInstalled = ForgeMachineRegistrationApi.Install(report.KnownBuild,
            log.Callback);
        var inventoryDragProbeInstalled = log.IsDebugEnabled && InventoryDragFaultProbe.Install(
            report.KnownBuild, log.Callback);
        ForgeCapabilities.Publish(new(report.KnownBuild, nativeNodeInstalled,
            NativeItemRegistry.ModuleHookInstalled,
            NativeItemRegistry.AmenityHookInstalled,
            NativeShopAdapter.Installed,
            report.KnownBuild && report.RunDataSignaturesMatch,
            report.KnownBuild && report.InventoryReadSignaturesMatch,
            report.KnownBuild && report.InventoryPreviewSignaturesMatch,
            nativeEffectInstalled, false, machineInstalled));
        log.Info($"[NicokoboForge/P0] readOnlyProbesInstalled={installed}; " +
            $"nativeNodeHookInstalled={nativeNodeInstalled}; stagedNodes={NativeItemRegistry.StagedCount}; " +
            $"moduleHookInstalled={NativeItemRegistry.ModuleHookInstalled}; " +
            $"amenityHookInstalled={NativeItemRegistry.AmenityHookInstalled}; " +
            $"nightShopHookInstalled={NativeShopAdapter.Installed}; " +
            $"nativeEffectHookInstalled={nativeEffectInstalled}; " +
            $"machineHooksInstalled={machineInstalled}; " +
            $"inventoryDragProbeInstalled={inventoryDragProbeInstalled}; " +
            $"stagedEffects={NativeEffectRegistry.StagedCount}; " +
            $"declarations={ForgeApi.Snapshot().Count}");
    }
}
