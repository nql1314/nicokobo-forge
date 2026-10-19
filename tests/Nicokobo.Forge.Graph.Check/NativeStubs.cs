using System.Text.Json;
using System.Text.Json.Serialization;

namespace Il2CppInterop.Runtime
{
    public static class Il2CppType { public static Type Of<T>() => typeof(T); }
    public static class Casts { public static T Cast<T>(this object value) => (T)value; }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppStructArray<T>(T[] data)
    { public T[] Data = data; public T[] ToArray() => Data; public static implicit operator T[](Il2CppStructArray<T> data) => data.Data; }
}
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace Il2CppES3Internal { public static class ES3TypeMgr { public static Type GetOrCreateES3Type(Type type, bool throwException) => type; } }
namespace UnityEngine
{
    public static class JsonUtility
    {
        internal static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        public static string ToJson(object data) => JsonSerializer.Serialize(data, data.GetType(), Options);
        public static object FromJson(string json, Type type) => JsonSerializer.Deserialize(json, type, Options)!;
    }
}
namespace Il2Cpp
{
    public interface GraphNodeStorage { }
    public sealed class ItemFeature
    {
        public string identifier = "condition";
        public int parentItemUniqueId, actualValue;
        public bool isDisabled;
        [JsonIgnore] public IntPtr Pointer => new(parentItemUniqueId);
    }
    public sealed class GameItem : GraphNodeStorage
    {
        private static int _nextPointer;
        public string identifier = "fanny_pack";
        public int uniqueId, unitCount = 1;
        public IntPtr Pointer = new(Interlocked.Increment(ref _nextPointer));
        public object? parentInventory;
        public System.Collections.Generic.List<GameItem> Nested = new();
        public Il2CppSystem.Collections.Generic.List<ItemFeature> itemFeatures = new();
        public Action<GameItem>? onLoaded;
        public int GetUniqueID() => uniqueId;
        public void AddItemFeature(ItemFeature feature) { feature.parentItemUniqueId = uniqueId; itemFeatures.Add(feature); }
        public void Destroy() => Pointer = IntPtr.Zero;
    }
    public sealed class PlayerStore
    {
        public static PlayerStore? instance;
        public IntPtr Pointer = new(1);
        public Il2CppSystem.Collections.Generic.List<ItemFeature>? savedItemFeatureList;
    }
    public sealed class SaveItemNode
    {
        public string identifier = "";
        public int uniqueId, unitCount, parentId;
        [JsonIgnore] public GameItem? tempLink;
    }
    public sealed class SaveState { public Il2CppSystem.Collections.Generic.List<SaveItemNode> saveItems = new(); }
    public static class GraphUtils
    {
        public static System.Collections.Generic.List<T> FindAllChildrenType<T>(GraphNodeStorage root, object? a, object? b) =>
            Walk((GameItem)root).Skip(1).Cast<T>().ToList();
        internal static IEnumerable<GameItem> Walk(GameItem root) => root.Nested.SelectMany(Walk).Prepend(root);
    }
    public static class SaveManager
    {
        public static Il2CppSystem.Collections.Generic.List<SaveItemNode> EncodeNodes(GraphNodeStorage root)
        {
            var result = new Il2CppSystem.Collections.Generic.List<SaveItemNode>();
            foreach (var item in GraphUtils.Walk((GameItem)root))
            {
                foreach (var feature in item.itemFeatures) PlayerStore.instance!.savedItemFeatureList!.Add(feature);
                var parent = GraphUtils.Walk((GameItem)root).FirstOrDefault(x => x.Nested.Contains(item));
                result.Add(new() { identifier = item.identifier, uniqueId = item.uniqueId, unitCount = item.unitCount, parentId = parent?.uniqueId ?? 0 });
            }
            return result;
        }
        public static GameItem DecodeNodes(Il2CppSystem.Collections.Generic.List<SaveItemNode> nodes)
        {
            foreach (var node in nodes) node.tempLink = new() { identifier = node.identifier, uniqueId = node.uniqueId, unitCount = node.unitCount };
            foreach (var node in nodes.Where(x => x.parentId != 0)) nodes.Single(x => x.uniqueId == node.parentId).tempLink!.Nested.Add(node.tempLink!);
            return nodes.Single(x => x.parentId == 0).tempLink!;
        }
    }
    public static class DirectoryMaster { public static bool Has<T>(string id) => id != "missing"; }
    public sealed class ES3Settings { public ES3Settings(bool defaults) { } }
    public static class ES3
    {
        internal static bool FailSerialize;
        public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> Serialize(object value, Type type, ES3Settings settings)
        { if (FailSerialize) throw new InvalidOperationException("serialization rejected"); return new(JsonSerializer.SerializeToUtf8Bytes(value, type, UnityEngine.JsonUtility.Options)); }
        public static object Deserialize(Type type, byte[] bytes, ES3Settings settings) => JsonSerializer.Deserialize(bytes, type, UnityEngine.JsonUtility.Options)!;
    }
    public static class MachineHelper { public static void LoadUpgrade(GameItem item) { } public static void OnLoad(GameItem item) { } }
    public static class SpriteHelper { public static void UpdateWaterContainerSprite(GameItem item) { } }
    public static class GeneralHelper
    { public static void LoadLockItem(GameItem item) { } public static void LoadNonOwnedToolTag(GameItem item) { } public static void LoadHazardousWasteTag(GameItem item) { } }
    public static class LockHelper { public static void LoadLockedContainer(GameItem item) { } }
    public static class MachineBrokenHelper { public static void LoadBrokenMachine(GameItem item) { } }
    public static class MachineTurboBoosterAdv { public static void LoadTurboBoostEligibleTag(GameItem item) { } }
}
