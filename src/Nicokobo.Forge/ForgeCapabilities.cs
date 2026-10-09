namespace Nicokobo.Forge;

public sealed record ForgeCapabilitySnapshot(bool KnownGameBuild,
    bool NativeItemDirectoryHook, bool NativeModuleDirectoryHook,
    bool NativeAmenityDirectoryHook, bool NightShopStock,
    bool RunDataStaging,
    bool DirectInventoryRead, bool WholeTransferPreview,
    bool NativeEffectRegistration, bool InventoryTransfer,
    bool MachineNightProcessing)
{
    public bool NpcTradeStock { get; init; }
    public bool DossierExpansion { get; init; }
    public bool PersistentInventoryEndpoints { get; init; }
    public bool ExternalPowerTransactions { get; init; }
    public bool BranchingDialogue { get; init; }
    public bool ClientVisits { get; init; }
    public bool ReadingWindow { get; init; }
}

/// <summary>Read-only capability report for content Mods. A true gate means the
/// adapter is installed, not that a particular Mod item or save has succeeded.</summary>
public static class ForgeCapabilities
{
    private static ForgeCapabilitySnapshot _current =
        new(false, false, false, false, false, false, false, false, false, false,
            false);

    public static ForgeCapabilitySnapshot Current =>
        System.Threading.Volatile.Read(ref _current);

    internal static void Publish(ForgeCapabilitySnapshot snapshot) =>
        System.Threading.Volatile.Write(ref _current, snapshot);
}
