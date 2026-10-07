using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using MelonLoader.Utils;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class MissingItemSaveRuntime
{
    internal const string RecoveryKey = "nicokobo.forge.save.recovered_items_v1";
    internal const string ProviderKey = "nicokobo.forge.save.item_providers_v1";
    private const string HookId = "nicokobo.forge.save.missing_items";
    private static Action<string>? _log;
    [ThreadStatic] private static PlayerStore? _loading;
    private static bool _delivering;

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) return;
        bool installed = NativeHookSet.Install(HookId,
        [
            new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void),
                typeof(MissingItemSaveRuntime), nameof(BeforeLoad), nameof(AfterLoad), nameof(FinishLoad)),
            new(typeof(ModHook), nameof(ModHook.FireOnGameLoadedNormal), [], typeof(void),
                typeof(MissingItemSaveRuntime), Postfix: nameof(BeforeInventories)),
            new(typeof(PlayerStore), nameof(PlayerStore.SaveGame), [], typeof(void),
                typeof(MissingItemSaveRuntime), Prefix: nameof(BeforeSave))
        ], log);
        if (installed) log("[INFO] [NicokoboForge/SaveCompatibility] verified provider removal cleanup and persisted normal-item recovery enabled");
    }

    internal static void Uninstall() { _loading = null; NativeHookSet.Remove(HookId, _log); }
    private static void BeforeLoad(PlayerStore __instance) => _loading = __instance;
    private static void AfterLoad(PlayerStore __instance, bool __runOriginal)
    {
        _loading = null;
        if (__runOriginal) Deliver(__instance);
    }
    private static Exception? FinishLoad(Exception? __exception) { _loading = null; return __exception; }
    private static void BeforeSave(PlayerStore __instance)
    {
        if (_loading != null || !Current(__instance)) return;
        Deliver(__instance);
        RememberProviders(__instance);
    }

    // On this supported build, scalar ES3 fields have loaded before Normal;
    // all saved inventories decode immediately afterward. No file writes here.
    private static void BeforeInventories(bool __runOriginal)
    {
        var store = _loading;
        if (!__runOriginal || !Current(store)) return;
        try
        {
            var providers = ReadProviders(store!);
            if (providers.Count == 0) return;
            var inventories = Capture(store!);
            var pending = ReadRecoveries(store!);
            var declarations = NativeItemRegistry.Declarations().Select(item => item.ItemId).ToHashSet(StringComparer.Ordinal);
            var (assemblies, readable) = InstalledAssemblies();
            var retained = new HashSet<string>(StringComparer.Ordinal);
            bool Keep(string id, string type)
            {
                // No Forge provenance: do not even classify another Mod's ID
                // against our directory assumptions. Native load owns it.
                if (!providers.ContainsKey(id)) return true;
                bool registered = Registered(id, type);
                bool missing = MissingItemSavePolicy.ConfirmedMissing(id, type, registered,
                    providers, declarations, assemblies, readable);
                if (!registered && !missing) retained.Add(id);
                return !missing;
            }
            var plan = MissingItemSaveMigration.Plan(inventories, pending, store!.playerCash, Keep);
            if (retained.Count != 0)
                _log?.Invoke($"[WARN] [NicokoboForge/SaveCompatibility] Forge-registered IDs retained; provider removal not confirmed; count={retained.Count}; ids={string.Join(",", retained.OrderBy(id => id, StringComparer.Ordinal).Take(8))}");
            if (plan.RemovedNodes == 0) return;
            int cashBefore = store.playerCash;
            var oldData = store.modData;
            bool hadRecovery = oldData != null && oldData.ContainsKey(RecoveryKey);
            string? oldRecovery = hadRecovery ? oldData![RecoveryKey] : null;
            try
            {
                foreach (var (key, json) in plan.Inventories) Set(store, key, json);
                WriteRecoveries(store, plan.Recoveries);
                store.playerCash = plan.Cash;
            }
            catch
            {
                foreach (string key in plan.Inventories.Keys) Set(store, key, inventories[key]);
                store.playerCash = cashBefore;
                store.modData = oldData;
                if (oldData != null)
                {
                    if (hadRecovery) oldData[RecoveryKey] = oldRecovery;
                    else oldData.Remove(RecoveryKey);
                }
                throw;
            }
            _log?.Invoke($"[INFO] [NicokoboForge/SaveCompatibility] missing nodes removed={plan.RemovedNodes}; refunded credits={plan.Refund}; normal item recoveries={plan.Recoveries.Count}; next normal save persists changes");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/SaveCompatibility] cleanup rejected: {ex.Message}");
        }
    }

    private static bool Registered(string id, string itemType)
    {
        if (itemType == nameof(GameCharacterItem) || itemType.EndsWith("." + nameof(GameCharacterItem), StringComparison.Ordinal))
            return DirectoryMaster.Has<GameCharacterItem>(id);
        // Let the existing terminal migration normalize these historical IDs.
        return DirectoryMaster.Has<GameItem>(ForgeManufacturingTerminal.NormalizeItemId(id));
    }

    private static Dictionary<string, string> ReadProviders(PlayerStore store) =>
        MissingItemSavePolicy.ReadProviders(store.modData != null && store.modData.ContainsKey(ProviderKey)
            ? store.modData[ProviderKey] : null);

    private static void RememberProviders(PlayerStore store)
    {
        try
        {
            var providers = ReadProviders(store);
            var loaded = MelonMod.RegisteredMelons.Select(mod => mod.MelonAssembly.Assembly.GetName().Name)
                .Where(name => name != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            foreach (var (id, assembly) in NativeItemRegistry.AppliedSaveProviders())
            {
                if (!loaded.Contains(assembly) || !Registered(id, nameof(GameItem))) continue;
                if (providers.TryGetValue(id, out var previous) && previous == assembly) continue;
                providers[id] = assembly;
                changed = true;
            }
            if (!changed) return;
            store.modData ??= new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
            store.modData[ProviderKey] = JsonSerializer.Serialize(providers);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/SaveCompatibility] item provider recording rejected: {ex.Message}");
        }
    }

    private static (IReadOnlySet<string> Names, bool Readable) InstalledAssemblies()
    {
        var names = MelonMod.RegisteredMelons.Select(mod => mod.MelonAssembly.Assembly.GetName().Name)
            .Where(name => name != null).Select(name => name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool readable = true;
        try
        {
            // A DLL can be installed but fail to load. Its mere absence from
            // RegisteredMelons must not authorize deleting its saved items.
            foreach (string file in Directory.EnumerateFiles(MelonEnvironment.ModsDirectory, "*.dll"))
            {
                try
                {
                    string? name = AssemblyName.GetAssemblyName(file).Name;
                    if (string.IsNullOrWhiteSpace(name)) readable = false;
                    else names.Add(name);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
                {
                    readable = false;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            readable = false;
        }
        if (!readable)
            _log?.Invoke("[WARN] [NicokoboForge/SaveCompatibility] installed DLL identities could not be fully read; unregistered items will be retained");
        return (names, readable);
    }
    private static bool Current(PlayerStore? store) => store != null && store.Pointer != IntPtr.Zero &&
        PlayerStore.instance?.Pointer == store.Pointer && store.saveSlotId >= 0 && !string.IsNullOrWhiteSpace(store.runID);
    private static IReadOnlyList<string> ReadRecoveries(PlayerStore store) =>
        store.modData != null && store.modData.ContainsKey(RecoveryKey)
            ? JsonSerializer.Deserialize<string[]>(store.modData[RecoveryKey])
                ?? throw new InvalidDataException("Null recovery data")
            : [];
    private static void WriteRecoveries(PlayerStore store, IReadOnlyList<string> recoveries)
    {
        if (recoveries.Count == 0) { store.modData?.Remove(RecoveryKey); return; }
        store.modData ??= new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
        store.modData[RecoveryKey] = JsonSerializer.Serialize(recoveries);
    }

    private static void Deliver(PlayerStore store)
    {
        if (_delivering || !Current(store)) return;
        _delivering = true;
        try
        {
            var pending = ReadRecoveries(store).ToList();
            if (pending.Count == 0) return;
            var destinations = Destinations(store);
            int delivered = 0;
            for (int i = 0; i < pending.Count;)
            {
                GameItem? item = null;
                bool attached = false;
                try
                {
                    var state = UnityEngine.JsonUtility.FromJson(pending[i], Il2CppType.Of<SaveState>()).Cast<SaveState>();
                    if (state?.saveItems == null || state.saveItems.Count == 0)
                        throw new InvalidDataException("Empty recovery graph");
                    var root = state.saveItems[0];
                    // A native insertion callback may save before returning.
                    // In that file both the placed item and its old receipt can
                    // coexist: the live identity wins, never recreate a second copy.
                    foreach (var existing in store.FindAllItem(true))
                        if (existing.uniqueId == root.uniqueId)
                        {
                            if (existing.identifier != ForgeManufacturingTerminal.NormalizeItemId(root.identifier))
                                throw new InvalidDataException("Conflicting recovery identity: " + root.uniqueId);
                            attached = true;
                            break;
                        }
                    if (attached) continue;
                    foreach (var node in state.saveItems)
                        if (!Registered(node.identifier, node.itemType))
                            throw new InvalidDataException("Recovery factory became unavailable");
                    item = SaveManager.DecodeNodes(state.saveItems);
                    if (item == null || item.Pointer == IntPtr.Zero || item.parentInventory != null || item.unitCount <= 0)
                        throw new InvalidDataException("Invalid recovered item");
                    RestoreLoadState(state);
                    int units = item.unitCount;
                    foreach (var destination in destinations)
                    {
                        var plan = ForgeInventoryPlacementApi.PlanWholeStacks(destination, [item]);
                        if (plan == null) continue;
                        var slot = destination.TryInventorySlot(item, units, plan[0].Shape);
                        if (slot == null || slot.Pointer == IntPtr.Zero || slot.item?.Pointer != item.Pointer ||
                            slot.inventory?.Pointer != destination.Pointer || slot.targetItem != null ||
                            slot.numTransfer != units || !slot.IsValid() || item.parentInventory != null ||
                            item.unitCount != units) continue;
                        try { slot.TryAcceptOnce(units); }
                        catch (Exception ex) { _log?.Invoke($"[WARN] [NicokoboForge/SaveCompatibility] placement callback: {ex.Message}"); }
                        if (item.parentInventory != null)
                        {
                            attached = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"[WARN] [NicokoboForge/SaveCompatibility] recovery retained: {ex.Message}");
                }
                finally
                {
                    // Receipt removal and native inventory placement share the
                    // next normal SaveGame write. Full inventories keep the graph.
                    attached |= item?.parentInventory != null;
                    if (attached)
                    {
                        pending.RemoveAt(i);
                        WriteRecoveries(store, pending);
                        delivered++;
                    }
                    else
                    {
                        if (item != null && item.Pointer != IntPtr.Zero) item.Destroy();
                        i++;
                    }
                }
            }
            if (delivered != 0 || pending.Count != 0)
                _log?.Invoke($"[INFO] [NicokoboForge/SaveCompatibility] normal items returned={delivered}; awaiting inventory space={pending.Count}");
        }
        catch (Exception ex) { _log?.Invoke($"[ERROR] [NicokoboForge/SaveCompatibility] recovery paused: {ex.Message}"); }
        finally { _delivering = false; }
    }

    private static IReadOnlyList<GameGridInventory> Destinations(PlayerStore store)
    {
        var result = new List<GameGridInventory>();
        var seen = new HashSet<IntPtr>();
        void Add(GameGridInventory? inventory)
        {
            if (inventory != null && inventory.Pointer != IntPtr.Zero && seen.Add(inventory.Pointer)) result.Add(inventory);
        }
        Add(EmporiumEntry.Instance?.invElement);
        Add(EmporiumEntry.Instance?.backInvinvElement);
        foreach (var item in store.FindAllItem(true))
        {
            if (!GeneralHelper.IsItemOwned(item) || !(item.IsGameItemType("STORAGE") || item.IsTag("backpack"))) continue;
            foreach (var child in item.children)
                try { Add(child.TryCast<GameGridInventory>()); } catch (InvalidCastException) { }
        }
        return result.AsReadOnly();
    }

    private static void RestoreLoadState(SaveState state)
    {
        // Match PlayerStore.DecodeSaveItem's native load fixups; factory creation
        // alone does not restore machine upgrades, container locks or callbacks.
        foreach (var node in state.saveItems)
        {
            var item = node.tempLink;
            if (item == null) continue;
            MachineHelper.LoadUpgrade(item);
            MachineHydroponic.LoadHydroponicModuleInvData(item);
            SpriteHelper.UpdateWaterContainerSprite(item);
            GeneralHelper.LoadLockItem(item);
            GeneralHelper.LoadNonOwnedToolTag(item);
            GeneralHelper.LoadHazardousWasteTag(item);
            LockHelper.LoadLockedContainer(item);
            MachineBrokenHelper.LoadBrokenMachine(item);
            MachineHelper.OnLoad(item);
            MachineTurboBoosterAdv.LoadTurboBoostEligibleTag(item);
            item.onLoaded?.Invoke(item);
        }
    }

    private static Dictionary<string, string?> Capture(PlayerStore store) => new(StringComparer.Ordinal)
    {
        ["mainInvJSON"] = store.mainInvJSON, ["backInvJSON"] = store.backInvJSON,
        ["trashcanInvJSON"] = store.trashcanInvJSON, ["docInvJSON"] = store.docInvJSON,
        ["cassettePlayerInvJSON"] = store.cassettePlayerInvJSON, ["showcaseInvJSON"] = store.showcaseInvJSON,
        ["soldInvJSON"] = store.soldInvJSON, ["hirelingInvJSON"] = store.hirelingInvJSON,
        ["hiddenInvJSON"] = store.hiddenInvJSON, ["vendingMachineInvJSON"] = store.vendingMachineInvJSON,
        ["vendingFountainInvJSON"] = store.vendingFountainInvJSON
    };
    private static void Set(PlayerStore store, string key, string? value)
    {
        switch (key)
        {
            case "mainInvJSON": store.mainInvJSON = value; break;
            case "backInvJSON": store.backInvJSON = value; break;
            case "trashcanInvJSON": store.trashcanInvJSON = value; break;
            case "docInvJSON": store.docInvJSON = value; break;
            case "cassettePlayerInvJSON": store.cassettePlayerInvJSON = value; break;
            case "showcaseInvJSON": store.showcaseInvJSON = value; break;
            case "soldInvJSON": store.soldInvJSON = value; break;
            case "hirelingInvJSON": store.hirelingInvJSON = value; break;
            case "hiddenInvJSON": store.hiddenInvJSON = value; break;
            case "vendingMachineInvJSON": store.vendingMachineInvJSON = value; break;
            case "vendingFountainInvJSON": store.vendingFountainInvJSON = value; break;
            default: throw new ArgumentException("Unknown saved inventory: " + key);
        }
    }
}
