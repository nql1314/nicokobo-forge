using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

namespace Nicokobo.Forge.Diagnostics;

internal sealed record BuildProbeReport(bool KnownBuild, bool SignaturesMatch,
    bool MiscDirectorySignatureMatch, bool ModuleDirectorySignaturesMatch,
    bool AmenityDirectorySignatureMatch, bool NightShopSignaturesMatch,
    bool NetworkUpgradeSignaturesMatch,
    bool RandomEffectSignatureMatch,
    bool RunDataSignaturesMatch, bool InventoryReadSignaturesMatch,
    bool InventoryPreviewSignaturesMatch, bool NativeEffectSignaturesMatch,
    IReadOnlyList<string> Lines)
{
    internal bool CanInstallReadOnlyProbes => KnownBuild && SignaturesMatch;
}

internal static class BuildProbe
{
    private const string KnownGameSha256 =
        "3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44";
    private const string KnownInteropSha256 =
        "9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC";
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    internal static BuildProbeReport Run(string? gameRoot)
    {
        var lines = new List<string>();
        try
        {
            string gameHash = Hash(gameRoot == null ? null : Path.Combine(gameRoot, "GameAssembly.dll"));
            string interopHash = Hash(gameRoot == null ? null : Path.Combine(gameRoot,
                "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));
            bool knownBuild = gameHash == KnownGameSha256 && interopHash == KnownInteropSha256;
            lines.Add($"[NicokoboForge/P0] gameBuild=25382790; knownBuild={knownBuild}; GameAssembly.sha256={gameHash}; Assembly-CSharp.sha256={interopHash}");
            lines.Add($"[NicokoboForge/P0] Loader={typeof(MelonLoader.MelonMod).Assembly.GetName().Version}; Harmony={typeof(HarmonyLib.Harmony).Assembly.GetName().Version}; Interop={typeof(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase).Assembly.GetName().Version}");

            bool miscDirectorySignature = Check(lines, typeof(Il2Cpp.MiscItemDirectory),
                "InitDirectory", false, "System.Void");
            bool signatures = miscDirectorySignature;
            bool moduleDirectorySignatures = Check(lines,
                typeof(Il2Cpp.ModuleDirectory),
                "InitDirectory", false, "System.Void");
            bool amenityDirectorySignature = Check(lines,
                typeof(Il2Cpp.AmenitiesItemDirectory),
                "InitDirectory", false, "System.Void");
            bool nightShopSignatures = Check(lines, typeof(Il2Cpp.StoreClientList),
                "PlaceInventorInventory", true, "System.Void", "System.Boolean") &
                Check(lines, typeof(Il2Cpp.PlayerStore),
                    "OnItemBought", false, "System.Void", "Il2Cpp.GameItem",
                    "System.Int32") &
                Check(lines, typeof(Il2Cpp.PlayerStore),
                    "IsPlayerOwnThisItem", false, "System.Boolean", "System.String") &
                Check(lines, typeof(Il2Cpp.PlayerStore),
                    "AddDirectSellingItemToTable", false, "System.Void",
                    "Il2Cpp.GameItem", "System.Boolean", "System.Boolean",
                    "System.Boolean", "System.Int32");
            bool networkUpgradeSignatures =
                CheckConstructor(lines, typeof(Il2Cpp.NetworkUpgrade), "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "InitUpgradeDict", true,
                    typeof(Il2CppSystem.Collections.Generic.Dictionary<string,
                        Il2Cpp.NetworkUpgrade>).FullName!) &
                Check(lines, typeof(Il2Cpp.ModHook),
                    "FireOnGameLoadedLate", true, "System.Void") &
                Check(lines, typeof(Il2Cpp.WildUIManager),
                    "OpenUI", false, "System.Void", "System.Boolean") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetLocalizedTitle", false, "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetLocalizedSubtitle", false, "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetLocalizedDescription", false, "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetLocalizedAlreadyBought", false, "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetCost", false, "System.Int32") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "IsReady", false, "System.Boolean") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "GetMissingPrerequisite", false, "System.String") &
                Check(lines, typeof(Il2Cpp.NetworkUpgrade),
                    "Unlock", false, "System.Void") &
                Check(lines, typeof(Il2Cpp.WildUIManager),
                    "OnUnlockClicked", false, "System.Void") &
                Check(lines, typeof(Il2Cpp.NetworkElement),
                    "UpdateElement", false, "System.Void") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.PlayerStore),
                    "networkUpgrade", "Il2CppSystem.Collections.Generic.Dictionary`2") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.WildUIManager),
                    "networkElements", "Il2CppSystem.Collections.Generic.List`1") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkElement),
                    "id", "System.String") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkUpgrade),
                    "id", "System.String") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkUpgrade),
                    "cost", "System.Int32") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkUpgrade),
                    "prerequisite", "Il2CppSystem.Collections.Generic.List`1") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkUpgrade),
                    "state", "System.Int32") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.WildUIManager),
                    "currentSelectedId", "System.String") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.PlayerStore),
                    "playerCash", "System.Int32") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.PlayerStore),
                    "wildFavor", "System.Int32") &
                CheckProperty(lines, "Network", typeof(Il2Cpp.NetworkUpgradeList),
                    "UnlockActions", "Il2CppSystem.Collections.Generic.Dictionary`2");
            signatures &= Check(lines, typeof(Il2Cpp.PerkUIController),
                "OpenUI", false, "System.Void");
            signatures &= Check(lines, typeof(Il2Cpp.PlayerStore),
                "InitialSave", false, "System.Void");
            signatures &= Check(lines, typeof(Il2Cpp.ModHook),
                "FireOnGameLoadedLate", true, "System.Void");
            bool randomEffectSignature = Check(lines, typeof(Il2Cpp.ModuleEffectHelper),
                "InitRandomEffect", true, "System.Void", "Il2Cpp.GameItem", "System.Int32");
            signatures &= randomEffectSignature;
            signatures &= Check(lines, typeof(Il2Cpp.StartingPerkList),
                "InitStartingPerk", true, "System.Void");
            bool runDataSignatures = CheckProperty(lines, "RunData", typeof(Il2Cpp.PlayerStore),
                "runID", "System.String") &
                CheckProperty(lines, "RunData", typeof(Il2Cpp.PlayerStore),
                    "saveSlotId", "System.Int32") &
                CheckProperty(lines, "RunData", typeof(Il2Cpp.PlayerStore), "modData",
                    "Il2CppSystem.Collections.Generic.Dictionary`2");
            bool inventoryReadSignatures =
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.GameInventory), "childItems",
                    "Il2CppSystem.Collections.Generic.List`1") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.GameItem), "identifier",
                    "System.String") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.GameItem), "uniqueId",
                    "System.Int32") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.GameItem), "unitCount",
                    "System.Int32") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.GameItem), "parentInventory",
                    "Il2Cpp.GameInventory") &
                Check(lines, typeof(Il2Cpp.GameItem), "IsTag", false,
                    "System.Boolean", "System.String");
            bool inventoryPreviewSignatures = inventoryReadSignatures &
                Check(lines, typeof(Il2Cpp.GameInventory), "IsRemoveLocked", false,
                    "System.Boolean") &
                Check(lines, typeof(Il2Cpp.GameInventory), "IsInsertLocked", false,
                    "System.Boolean") &
                Check(lines, typeof(Il2Cpp.GameItem), "MaxNumRemove", false,
                    "System.Int32") &
                Check(lines, typeof(Il2Cpp.GameGridInventory),
                    "TryFindOneValidInventorySlot", false, "Il2Cpp.SlotMarker",
                    "Il2Cpp.GameItem", "System.Boolean") &
                Check(lines, typeof(Il2Cpp.SlotMarker), "IsValid", false,
                    "System.Boolean") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.SlotMarker),
                    "item", "Il2Cpp.GameItem") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.SlotMarker),
                    "inventory", "Il2Cpp.GameInventory") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.SlotMarker),
                    "targetItem", "Il2Cpp.GameItem") &
                CheckProperty(lines, "Inventory", typeof(Il2Cpp.SlotMarker),
                    "numTransfer", "System.Int32");
            var effectRegistry = typeof(Il2Cpp.ModuleEffectHelper).GetProperty(
                "moduleEffects", Declared);
            bool effectRegistryMatch = effectRegistry != null &&
                effectRegistry.GetMethod?.IsStatic == true &&
                effectRegistry.PropertyType.IsGenericType &&
                effectRegistry.PropertyType.GetGenericTypeDefinition().FullName ==
                    "Il2CppSystem.Collections.Generic.Dictionary`2";
            lines.Add($"[NicokoboForge/Effect] Il2Cpp.ModuleEffectHelper.moduleEffects:" +
                $"{effectRegistry?.PropertyType.FullName ?? "MISSING"}; match={effectRegistryMatch}");
            bool nativeEffectSignatures = effectRegistryMatch &
                CheckProperty(lines, "Effect",
                    typeof(Il2Cpp.ModuleEffectHelper.ModuleEffect),
                    "identifier", "System.String");
            lines.Add($"[NicokoboForge/P0] requiredSignaturesMatch={signatures}; " +
                $"nativeRegistrationAllowed={knownBuild && miscDirectorySignature}");
            lines.Add($"[NicokoboForge/Module] directorySignatureMatch={moduleDirectorySignatures}; " +
                $"moduleRegistrationAllowed={knownBuild && miscDirectorySignature && moduleDirectorySignatures}");
            lines.Add($"[NicokoboForge/Amenity] directorySignatureMatch={amenityDirectorySignature}; " +
                $"amenityRegistrationAllowed={knownBuild && amenityDirectorySignature}");
            lines.Add($"[NicokoboForge/NightShop] signaturesMatch={nightShopSignatures}; " +
                $"stockRegistrationAllowed={knownBuild && nightShopSignatures}");
            lines.Add($"[NicokoboForge/Network] signaturesMatch={networkUpgradeSignatures}; " +
                $"registrationAllowed={knownBuild && networkUpgradeSignatures}");
            lines.Add($"[NicokoboForge/RunData] signaturesMatch={runDataSignatures}; " +
                $"stageAllowed={knownBuild && runDataSignatures}");
            lines.Add($"[NicokoboForge/Inventory] readSignaturesMatch={inventoryReadSignatures}; " +
                $"readAllowed={knownBuild && inventoryReadSignatures}; " +
                $"previewSignaturesMatch={inventoryPreviewSignatures}; " +
                $"previewAllowed={knownBuild && inventoryPreviewSignatures}; transferAllowed=false");
            lines.Add($"[NicokoboForge/Effect] signaturesMatch={nativeEffectSignatures}; " +
                $"registrationAllowed={knownBuild && miscDirectorySignature && randomEffectSignature && nativeEffectSignatures}");
            return new(knownBuild, signatures, miscDirectorySignature,
                moduleDirectorySignatures, amenityDirectorySignature,
                nightShopSignatures, networkUpgradeSignatures, randomEffectSignature,
                runDataSignatures,
                inventoryReadSignatures, inventoryPreviewSignatures,
                nativeEffectSignatures, lines);
        }
        catch (Exception ex)
        {
            lines.Add($"[NicokoboForge/P0] probeFailed={ex.GetType().Name}: {ex.Message}");
            return new(false, false, false, false, false, false, false, false, false,
                false, false, false, lines);
        }
    }

    private static bool Check(List<string> lines, Type type, string name,
        bool isStatic, string returnType, params string[] arguments)
    {
        var matches = type.GetMethods(Declared).Where(method => method.Name == name).ToArray();
        var method = matches.SingleOrDefault(candidate =>
            candidate.IsStatic == isStatic && candidate.ReturnType.FullName == returnType &&
            candidate.GetParameters().Select(parameter => parameter.ParameterType.FullName)
                .SequenceEqual(arguments));
        bool found = method != null;
        string owners = found
            ? string.Join(",", (IEnumerable<string>?)HarmonyLib.Harmony.GetPatchInfo(method!)?.Owners
                ?? Array.Empty<string>())
            : "n/a";
        lines.Add($"[NicokoboForge/P0] {type.FullName}.{name}({string.Join(",", arguments)}):{returnType}; match={found}; patchOwners=[{owners}]");
        return found;
    }

    private static bool CheckConstructor(List<string> lines, Type type,
        params string[] arguments)
    {
        var constructors = type.GetConstructors(Declared);
        bool found = constructors.Any(candidate => candidate.GetParameters()
            .Select(parameter => parameter.ParameterType.FullName)
            .SequenceEqual(arguments));
        lines.Add($"[NicokoboForge/Network] {type.FullName}.ctor(" +
            $"{string.Join(",", arguments)}); match={found}");
        return found;
    }

    private static string Hash(string? path)
    {
        if (path == null || !File.Exists(path)) return "MISSING";
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static bool CheckProperty(List<string> lines, string area, Type type, string name,
        string expectedType)
    {
        var property = type.GetProperty(name, Declared);
        bool found = property != null && property.CanRead &&
            (property.PropertyType.FullName == expectedType ||
             property.PropertyType.IsGenericType &&
             property.PropertyType.GetGenericTypeDefinition().FullName == expectedType);
        lines.Add($"[NicokoboForge/{area}] {type.FullName}.{name}:" +
            $"{property?.PropertyType.FullName ?? "MISSING"}; match={found}");
        return found;
    }
}
