using System.Text.Json;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>A content-owned hidden root, saved once in normal SaveGame modData.
/// Visible containers already saved by the game must not register here.</summary>
public sealed record ForgeInventoryEndpoint(string OwnerId, string EndpointId, GameItem Root, bool ChargingOnly = false);

public static class ForgeInventoryEndpointApi
{
    private const string SaveKey = "nicokobo.forge.inventory.endpoints_v1";
    private sealed record SavedEndpoint(string OwnerId, string EndpointId, string ItemId, int InstanceId, string Graph, bool ChargingOnly = false);
    private static readonly Dictionary<string, ForgeInventoryEndpoint> Roots = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SavedEndpoint> Saved = new(StringComparer.Ordinal);
    private static PlayerStore? _store;
    private static string? _run;
    private static int _slot = -1;
    private static bool _loading, _read;
    private static Action<string>? _log;
    private static IDisposable? _loadedLease;
    public static bool IsAvailable { get; private set; }
    public static string? Error { get; private set; }

    /// <summary>Open the same per-run native root, or decode its full graph.
    /// A malformed, conflicting or unavailable graph is never replaced empty.</summary>
    public static ForgeInventoryEndpoint Open(PlayerStore store, string ownerId, string endpointId, string itemId, bool createIfMissing = false, bool chargingOnly = false)
    {
        if (!IsAvailable || _loading || store == null || store.Pointer == IntPtr.Zero ||
            store.saveSlotId < 0 || string.IsNullOrWhiteSpace(store.runID) ||
            !OwnedCallbacks<bool, bool>.ValidId(ownerId, endpointId) ||
            !OwnedCallbacks<bool, bool>.ValidId(ownerId, itemId))
            throw new InvalidOperationException("Persistent inventory endpoint unavailable");
        Bind(store);
        if (Error != null) throw new InvalidDataException(Error);
        if (Roots.TryGetValue(endpointId, out var existing))
        {
            if (existing.OwnerId != ownerId || existing.Root.identifier != itemId || existing.Root.parentInventory != null || existing.ChargingOnly != chargingOnly)
                throw new InvalidDataException("Persistent endpoint ownership changed");
            return existing;
        }
        GameItem root;
        if (Saved.TryGetValue(endpointId, out var saved))
        {
            if (saved.OwnerId != ownerId || saved.ItemId != itemId || saved.ChargingOnly != chargingOnly) throw new InvalidDataException("Saved endpoint ownership conflict");
            root = ForgeSaveGraphApi.Decode(saved.Graph, CaptureExistingIds(store));
            if (root.identifier != itemId || root.GetUniqueID() != saved.InstanceId)
            { root.Destroy(); throw new InvalidDataException("Saved endpoint root identity mismatch"); }
        }
        else if (createIfMissing) root = ForgeItemApi.Create(itemId);
        else throw new InvalidDataException("Saved endpoint graph is missing; refusing an empty replacement");
        if (root.parentInventory != null || !GeneralHelper.IsItemOwned(root))
            throw new InvalidDataException("Endpoint root must be a detached player-owned item");
        var endpoint = new ForgeInventoryEndpoint(ownerId, endpointId, root, chargingOnly);
        Roots.Add(endpointId, endpoint);
        return endpoint;
    }
    public static IReadOnlyList<ForgeInventoryEndpoint> Snapshot() => Roots.Values.OrderBy(root => root.EndpointId, StringComparer.Ordinal).ToArray();
    public static bool Delete(string ownerId, string endpointId)
    {
        if (!Roots.TryGetValue(endpointId, out var root) || root.OwnerId != ownerId ||
            ForgeSaveGraphApi.Items(root.Root).Count != 1) return false;
        root.Root.Destroy(); Roots.Remove(endpointId); Saved.Remove(endpointId); return true;
    }
    private static IReadOnlySet<int> CaptureExistingIds(PlayerStore store) => store.FindAllItem(true).ToArray()
        .Where(item => item != null && item.Pointer != IntPtr.Zero).Select(item => item.GetUniqueID()).ToHashSet();
    private static void Bind(PlayerStore store)
    {
        if (_store?.Pointer == store.Pointer && _run == store.runID && _slot == store.saveSlotId && _read) return;
        Clear(); _store = store; _run = store.runID; _slot = store.saveSlotId; _read = true;
        try
        {
            if (store.modData == null || !store.modData.ContainsKey(SaveKey)) return;
            var saved = JsonSerializer.Deserialize<SavedEndpoint[]>(store.modData[SaveKey])
                ?? throw new InvalidDataException("Null endpoint save");
            foreach (var endpoint in saved)
                if (!OwnedCallbacks<bool, bool>.ValidId(endpoint.OwnerId, endpoint.EndpointId) ||
                    !OwnedCallbacks<bool, bool>.ValidId(endpoint.OwnerId, endpoint.ItemId) ||
                    endpoint.InstanceId <= 0 || string.IsNullOrWhiteSpace(endpoint.Graph) || !Saved.TryAdd(endpoint.EndpointId, endpoint))
                    throw new InvalidDataException("Invalid or duplicate saved endpoint");
        }
        catch (Exception ex) { Error = "Persistent endpoint save rejected: " + ex.Message; }
    }
    private static void Clear()
    {
        foreach (var root in Roots.Values)
            try { if (root.Root.parentInventory == null) root.Root.Destroy(); } catch { }
        Roots.Clear(); Saved.Clear(); _read = false; Error = null;
    }
    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) return;
        try
        {
            // FireOnGameLoadedLate occurs inside LoadGame, before its postfix.
            // Prepare in the shared ordered dispatch, before content restores
            // endpoints; independent postfix order can reject Open or destroy
            // the roots that another postfix has just decoded.
            _loadedLease = ForgeLifecycleApi.Subscribe("nicokobo.forge.inventory",
                "nicokobo.forge.inventory.endpoints_loaded", ForgeLifecyclePhase.AfterGameLoadedLate,
                _ => AfterLoaded(), int.MinValue);
            IsAvailable = NativeHookSet.Install("nicokobo.forge.inventory.endpoints",
            [
                new(typeof(PlayerStore), nameof(PlayerStore.FindAllItem), [typeof(bool)], typeof(Il2CppSystem.Collections.Generic.List<GameItem>), typeof(ForgeInventoryEndpointApi), Postfix: nameof(AfterFindItems)),
                new(typeof(PlayerStore), nameof(PlayerStore.SaveGame), [], typeof(void), typeof(ForgeInventoryEndpointApi), Prefix: nameof(BeforeSave)),
                new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void), typeof(ForgeInventoryEndpointApi), nameof(BeforeLoad), nameof(AfterLoad), nameof(FinishLoad)),
                new(typeof(PowerHelper), nameof(PowerHelper.GetAllPowerSourceFourniture), [typeof(bool)], typeof(Il2CppSystem.Collections.Generic.List<GameItem>), typeof(ForgeInventoryEndpointApi), Postfix: nameof(AfterPowerFurniture)),
                new(typeof(PowerHelper), nameof(PowerHelper.ChargeAllRechargable), [typeof(Il2CppSystem.Collections.Generic.List<GameItem>)], typeof(void), typeof(ForgeInventoryEndpointApi), Prefix: nameof(BeforeChargeItems))
            ], log);
            if (!IsAvailable) { _loadedLease.Dispose(); _loadedLease = null; }
        }
        catch (Exception ex)
        {
            _loadedLease?.Dispose(); _loadedLease = null; IsAvailable = false;
            try { log("[WARN] [NicokoboForge/Endpoints] load preparation unavailable: " + ex.Message); }
            catch { }
        }
    }
    private static void AfterFindItems(PlayerStore __instance, bool __0, Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        if (_loading || __result == null || _store?.Pointer != __instance.Pointer || _run != __instance.runID || _slot != __instance.saveSlotId) return;
        var seen = __result.ToArray().Where(item => item != null).Select(item => item.GetUniqueID()).ToHashSet();
        foreach (var root in Roots.Values)
            foreach (var item in ForgeSaveGraphApi.Items(root.Root))
                if ((!__0 || GeneralHelper.IsItemOwned(item)) && seen.Add(item.GetUniqueID())) __result.Add(item);
    }
    private static bool BeforeSave(PlayerStore __instance)
    {
        if (_loading || !IsAvailable) return true;
        if (_store?.Pointer != __instance.Pointer || _run != __instance.runID || _slot != __instance.saveSlotId) return true;
        try
        {
            if (Error != null) throw new InvalidDataException(Error);
            var next = new Dictionary<string, SavedEndpoint>(Saved, StringComparer.Ordinal);
            foreach (var root in Roots.Values)
            {
                if (root.Root.parentInventory != null) throw new InvalidDataException("Endpoint root is also in a native inventory");
                next[root.EndpointId] = new(root.OwnerId, root.EndpointId, root.Root.identifier, root.Root.GetUniqueID(), ForgeSaveGraphApi.Encode(root.Root), root.ChargingOnly);
            }
            string json = JsonSerializer.Serialize(next.Values.OrderBy(root => root.EndpointId, StringComparer.Ordinal).ToArray());
            __instance.modData ??= new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
            __instance.modData[SaveKey] = json;
            if (__instance.modData[SaveKey] != json) throw new InvalidDataException("Endpoint staging readback failed");
            Saved.Clear(); foreach (var pair in next) Saved.Add(pair.Key, pair.Value);
            return true;
        }
        catch (Exception ex) { Error = ex.Message; _log?.Invoke("[ERROR] [NicokoboForge/Endpoints] normal save stopped: " + ex); return false; }
    }
    private static void BeforeLoad() => _loading = true;
    private static void AfterLoad() => _loading = false;
    private static Exception? FinishLoad(Exception? __exception) { _loading = false; return __exception; }
    private static void AfterLoaded()
    {
        Clear(); _store = null; _run = null; _slot = -1;
        // Native decode has reached its ready signal. A nested Open is now
        // valid even though the enclosing LoadGame body has not returned yet.
        _loading = false;
    }
    private static void AfterPowerFurniture(Il2CppSystem.Collections.Generic.List<GameItem> __result)
    {
        if (!Current() || __result == null) return;
        // This list also drives CheckAvailablePower and the later global
        // TryRemoveEnergy loop. Never publish managed regional batteries or
        // their containers as native global discharge sources.
        var excluded = ChargingOnlyItems().Select(item => item.Pointer).ToHashSet();
        for (int index = __result.Count - 1; index >= 0; index--)
            if (__result[index] != null && excluded.Contains(__result[index].Pointer)) __result.RemoveAt(index);
    }
    private static void BeforeChargeItems(ref Il2CppSystem.Collections.Generic.List<GameItem> __0)
    {
        if (!Current() || __0 == null) return;
        // HandlePower retains its original source list for global discharge.
        // Replace only this call's argument with a separate charging copy;
        // native ChargeAllRechargable calls ChargeBattery on each real item.
        var copy = new Il2CppSystem.Collections.Generic.List<GameItem>();
        var seen = new HashSet<IntPtr>();
        foreach (var item in __0.ToArray())
        { copy.Add(item); if (item != null) seen.Add(item.Pointer); }
        foreach (var item in ChargingOnlyItems())
            if (item != null && item.IsTag("power_source_item") && GeneralHelper.IsItemOwned(item) && seen.Add(item.Pointer)) copy.Add(item);
        __0 = copy;
    }
    private static bool Current() => !_loading && _store != null && _store.Pointer == PlayerStore.instance?.Pointer &&
        _run == _store.runID && _slot == _store.saveSlotId;
    private static IEnumerable<GameItem> ChargingOnlyItems() => Roots.Values.Where(endpoint => endpoint.ChargingOnly)
        .SelectMany(endpoint => ForgeSaveGraphApi.Items(endpoint.Root));
}

/// <summary>Native SaveItemNode graph codec and native load fixups. This is not
/// a summary/count serializer; missing nodes and conflicting UIDs are rejected.</summary>
public static class ForgeSaveGraphApi
{
    public static IReadOnlyList<GameItem> Items(GameItem root)
    {
        var result = GraphUtils.FindAllChildrenType<GameItem>(root.Cast<GraphNodeStorage>(), null, null)?.ToArray()
            ?? throw new InvalidDataException("Item graph unavailable");
        return result.Append(root).DistinctBy(item => item.Pointer).ToArray();
    }
    public static string Encode(GameItem root)
    {
        var items = Items(root);
        if (items.Any(item => item == null || item.Pointer == IntPtr.Zero || item.GetUniqueID() <= 0 || item.unitCount <= 0) ||
            items.Select(item => item.GetUniqueID()).Distinct().Count() != items.Count) throw new InvalidDataException("Invalid item graph");
        var nodes = SaveManager.EncodeNodes(root.Cast<GraphNodeStorage>());
        if (nodes == null || nodes.Count != items.Count || nodes.ToArray().Select(node => node.uniqueId).ToHashSet().SetEquals(items.Select(item => item.GetUniqueID())) == false)
            throw new InvalidDataException("Incomplete native item graph");
        return UnityEngine.JsonUtility.ToJson(new SaveState { saveItems = nodes });
    }
    public static GameItem Decode(string json, IReadOnlySet<int>? existingIds = null)
    {
        var state = UnityEngine.JsonUtility.FromJson(json, Il2CppType.Of<SaveState>()).Cast<SaveState>();
        if (state?.saveItems == null || state.saveItems.Count == 0 ||
            state.saveItems.ToArray().Any(node => node.uniqueId <= 0 || node.unitCount <= 0 ||
                string.IsNullOrWhiteSpace(node.identifier) || existingIds?.Contains(node.uniqueId) == true || !DirectoryMaster.Has<GameItem>(node.identifier)) ||
            state.saveItems.ToArray().Select(node => node.uniqueId).Distinct().Count() != state.saveItems.Count)
            throw new InvalidDataException("Invalid, unavailable or conflicting saved item graph");
        var root = SaveManager.DecodeNodes(state.saveItems);
        if (root == null || root.Pointer == IntPtr.Zero || root.parentInventory != null) throw new InvalidDataException("Invalid decoded graph root");
        try
        {
            foreach (var node in state.saveItems)
            {
                var item = node.tempLink ?? throw new InvalidDataException("Saved graph node not restored");
                if (item.identifier != node.identifier || item.GetUniqueID() != node.uniqueId || item.unitCount != node.unitCount)
                    throw new InvalidDataException("Decoded graph identity/count mismatch");
                MachineHelper.LoadUpgrade(item); MachineHydroponic.LoadHydroponicModuleInvData(item);
                SpriteHelper.UpdateWaterContainerSprite(item); GeneralHelper.LoadLockItem(item);
                GeneralHelper.LoadNonOwnedToolTag(item); GeneralHelper.LoadHazardousWasteTag(item);
                LockHelper.LoadLockedContainer(item); MachineBrokenHelper.LoadBrokenMachine(item);
                MachineHelper.OnLoad(item); MachineTurboBoosterAdv.LoadTurboBoostEligibleTag(item); item.onLoaded?.Invoke(item);
            }
            if (Items(root).Count != state.saveItems.Count) throw new InvalidDataException("Decoded graph is incomplete");
            return root;
        }
        catch { root.Destroy(); throw; }
    }
}
