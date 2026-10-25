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
            json["forgeItemFeatures"] = EncodeFeatures(store.savedItemFeatureList);
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
        var features = DecodeFeatures(json, state);
        var root = SaveManager.DecodeNodes(state.saveItems);
        if (root == null || root.Pointer == IntPtr.Zero || root.parentInventory != null) throw new InvalidDataException("Invalid decoded graph root");
        try
        {
            RestoreFeatures(state, features);
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

    // SaveItemNode does not contain ItemFeature data. Capture it before native
    // LoadGame clears its separate table, so a full inventory can defer delivery.
    internal static string CaptureRecoveryFeatures(string json, Il2CppSystem.Collections.Generic.List<ItemFeature>? savedFeatures)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        var nodes = document["saveItems"]?.AsArray() ?? throw new InvalidDataException("Missing recovery graph");
        var ids = nodes.Select(node => node?["uniqueId"]?.GetValue<int>()
            ?? throw new InvalidDataException("Missing recovery item ID")).ToHashSet();
        var embedded = ReadFeatures(document);
        // A pending receipt owns its feature table across saves. A later provider
        // removal can split that graph again; retain only the surviving nodes.
        var source = embedded ?? savedFeatures?.ToArray()
            ?? throw new InvalidDataException("Native recovery feature table unavailable");
        var features = new Il2CppSystem.Collections.Generic.List<ItemFeature>();
        foreach (var feature in source)
        {
            if (feature == null || feature.Pointer == IntPtr.Zero) throw new InvalidDataException("Invalid native feature");
            if (!ids.Contains(feature.parentItemUniqueId)) continue;
            if (string.IsNullOrWhiteSpace(feature.identifier)) throw new InvalidDataException("Missing native feature identifier");
            features.Add(feature);
        }
        if (embedded != null && features.Count == embedded.Length) return json;
        document["forgeItemFeatures"] = EncodeFeatures(features);
        return document.ToJsonString();
    }

    internal static ItemFeature[]? DecodeFeatures(string json, SaveState state)
    {
        var features = ReadFeatures(JsonNode.Parse(json)!.AsObject());
        var ids = state.saveItems.ToArray().Select(node => node.uniqueId).ToHashSet();
        if (features != null && features.Any(feature => feature == null || feature.Pointer == IntPtr.Zero ||
            !ids.Contains(feature.parentItemUniqueId) || string.IsNullOrWhiteSpace(feature.identifier)))
            throw new InvalidDataException("Native feature parent missing from item graph");
        return features;
    }

    internal static void RestoreFeatures(SaveState state, ItemFeature[]? features)
    {
        if (features == null) return;
        var byId = state.saveItems.ToArray().ToDictionary(node => node.uniqueId,
            node => node.tempLink ?? throw new InvalidDataException("Saved graph node not restored"));
        foreach (var item in byId.Values) item.itemFeatures.Clear();
        foreach (var feature in features) byId[feature.parentItemUniqueId].AddItemFeature(feature);
    }

    private static string EncodeFeatures(Il2CppSystem.Collections.Generic.List<ItemFeature> features)
    {
        var type = Il2CppES3Internal.ES3TypeMgr.GetOrCreateES3Type(Il2CppType.Of<Il2CppSystem.Collections.Generic.List<ItemFeature>>(), true);
        return Convert.ToBase64String(ES3.Serialize(features, type, new ES3Settings(false)).ToArray());
    }

    private static ItemFeature[]? ReadFeatures(JsonObject document)
    {
        if (!document.TryGetPropertyValue("forgeItemFeatures", out var payload)) return null;
        var bytes = Convert.FromBase64String(payload?.GetValue<string>() ?? throw new InvalidDataException("Missing native feature payload"));
        var type = Il2CppES3Internal.ES3TypeMgr.GetOrCreateES3Type(Il2CppType.Of<Il2CppSystem.Collections.Generic.List<ItemFeature>>(), true);
        return (ES3.Deserialize(type, new Il2CppStructArray<byte>(bytes), new ES3Settings(false))?.Cast<Il2CppSystem.Collections.Generic.List<ItemFeature>>()
            ?? throw new InvalidDataException("Native feature table unavailable")).ToArray();
    }
}
