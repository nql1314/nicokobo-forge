using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Text.Json.Nodes;

namespace Nicokobo.Forge;

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
        var store = PlayerStore.instance;
        if (store == null || store.Pointer == IntPtr.Zero) throw new InvalidOperationException("Native item graph requires an existing player context");
        var previousFeatures = store.savedItemFeatureList;
        // EncodeNode appends every feature to the player's pending save table.
        // Detached graphs need their own table, including before the first SaveGame.
        store.savedItemFeatureList = new Il2CppSystem.Collections.Generic.List<ItemFeature>();
        try
        {
            var nodes = SaveManager.EncodeNodes(root.Cast<GraphNodeStorage>());
            if (nodes == null || nodes.Count != items.Count || nodes.ToArray().Select(node => node.uniqueId).ToHashSet().SetEquals(items.Select(item => item.GetUniqueID())) == false)
                throw new InvalidDataException("Incomplete native item graph");
            var json = JsonNode.Parse(UnityEngine.JsonUtility.ToJson(new SaveState { saveItems = nodes }))!.AsObject();
            var featureType = Il2CppES3Internal.ES3TypeMgr.GetOrCreateES3Type(Il2CppType.Of<Il2CppSystem.Collections.Generic.List<ItemFeature>>(), true);
            var features = ES3.Serialize(store.savedItemFeatureList, featureType, new ES3Settings(false));
            json["forgeItemFeatures"] = Convert.ToBase64String(features.ToArray());
            return json.ToJsonString();
        }
        finally { store.savedItemFeatureList = previousFeatures; }
    }
    public static GameItem Decode(string json, IReadOnlySet<int>? existingIds = null)
    {
        var state = UnityEngine.JsonUtility.FromJson(json, Il2CppType.Of<SaveState>()).Cast<SaveState>();
        if (state?.saveItems == null || state.saveItems.Count == 0 ||
            state.saveItems.ToArray().Any(node => node.uniqueId <= 0 || node.unitCount <= 0 ||
                string.IsNullOrWhiteSpace(node.identifier) || existingIds?.Contains(node.uniqueId) == true || !DirectoryMaster.Has<GameItem>(node.identifier)) ||
            state.saveItems.ToArray().Select(node => node.uniqueId).Distinct().Count() != state.saveItems.Count)
            throw new InvalidDataException("Invalid, unavailable or conflicting saved item graph");
        ItemFeature[]? features = null;
        var jsonObject = JsonNode.Parse(json)!.AsObject();
        if (jsonObject.TryGetPropertyValue("forgeItemFeatures", out var payload))
        {
            var bytes = Convert.FromBase64String(payload?.GetValue<string>() ?? throw new InvalidDataException("Missing native feature payload"));
            var featureType = Il2CppES3Internal.ES3TypeMgr.GetOrCreateES3Type(Il2CppType.Of<Il2CppSystem.Collections.Generic.List<ItemFeature>>(), true);
            var decoded = ES3.Deserialize(featureType, new Il2CppStructArray<byte>(bytes), new ES3Settings(false))?.Cast<Il2CppSystem.Collections.Generic.List<ItemFeature>>()
                ?? throw new InvalidDataException("Native feature table unavailable");
            features = decoded.ToArray();
            var ids = state.saveItems.ToArray().Select(node => node.uniqueId).ToHashSet();
            if (features.Any(feature => feature == null || feature.Pointer == IntPtr.Zero ||
                !ids.Contains(feature.parentItemUniqueId) || string.IsNullOrWhiteSpace(feature.identifier)))
                throw new InvalidDataException("Native feature parent missing from item graph");
        }
        var root = SaveManager.DecodeNodes(state.saveItems);
        if (root == null || root.Pointer == IntPtr.Zero || root.parentInventory != null) throw new InvalidDataException("Invalid decoded graph root");
        try
        {
            if (features != null)
            {
                var byId = state.saveItems.ToArray().ToDictionary(node => node.uniqueId,
                    node => node.tempLink ?? throw new InvalidDataException("Saved graph node not restored"));
                foreach (var item in byId.Values) item.itemFeatures.Clear();
                foreach (var feature in features) byId[feature.parentItemUniqueId].AddItemFeature(feature);
            }
            foreach (var node in state.saveItems)
            {
                var item = node.tempLink ?? throw new InvalidDataException("Saved graph node not restored");
                if (item.identifier != node.identifier || item.GetUniqueID() != node.uniqueId || item.unitCount != node.unitCount)
                    throw new InvalidDataException("Decoded graph identity/count mismatch");
                // Build 26017150 removed the hydroponic-specific module-inventory restore;
                // the remaining generic machine fixups are the complete native load path.
                MachineHelper.LoadUpgrade(item);
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
