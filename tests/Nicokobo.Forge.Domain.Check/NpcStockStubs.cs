// Supply fixtures reuse the night-shop placement port, with native loot lists
// and deterministic RNG inputs. They do not emulate Harmony or native callbacks.
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { None, Succeeded, Failed }
}

namespace UnityEngine.Localization.Settings
{
    public sealed class Initialization
    {
        public bool Valid = true;
        public bool IsValid() => Valid;
        public bool IsDone { get; set; } = true;
        public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus Status { get; set; } =
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded;
    }
    public sealed class LocalizationSettings
    {
        public static bool HasSettings { get; set; } = true;
        public static LocalizationSettings Instance { get; } = new();
        public Initialization? m_InitializingOperationHandle = new();
    }
}

namespace Nicokobo.Forge
{
    public static class ForgeInventoryApi
    {
        public static readonly List<Il2Cpp.GameItem> OwnedItems = [];
        public static IReadOnlyList<Il2Cpp.GameItem> CaptureRunItems(Il2Cpp.PlayerStore store) => OwnedItems;
        public static bool IsPlayerOwned(Il2Cpp.GameItem item) => item.Owned;
    }
}

namespace Il2Cpp
{
    public static class StoreStation
    {
        public static int Day = 1;
        public static int GetDayCounter() => Day;
    }
    public sealed partial class GameItem
    {
        public string GameItemType = "ITEM";
        public bool IsGameItemType(string type) => GameItemType == type;
        public void SetUnitCount(int count) => unitCount = count;
    }
    public sealed class LootEntry(string itemId, float value, string source)
    {
        public string id = itemId;
        public float weight = value;
        public string sourceModId = source;
    }
    public static class LootRegistry
    {
        public static string RollInternal(string tableId, string groupId) => "";
        public static string Pick(Il2CppSystem.Collections.Generic.List<LootEntry> entries) => "";
    }
    public static class ItemSpawner
    {
        public static GameItem Spawn(string id) => new();
        public static GameItem SpawnFromTable(string id) => new();
        public static GameItem SpawnFromTableGroup(string id) => new();
    }
    public static class RNG
    {
        public static readonly Queue<double> Samples = new();
        public static int Calls;
        public static double GetRandomDouble(double min, double max)
        { Calls++; return Samples.Dequeue(); }
    }
    public static partial class StoreClientList
    {
        public static void PlaceSupplierInventory() { }
        public class __c;
        public class __c__DisplayClass22_0;
        public class __c__DisplayClass37_0;
        public class __c__DisplayClass41_0;
        public class __c__DisplayClass42_0;
    }
    public static class StoreClientListMinor { public class __c; }
    public static class StoreClientListRev { public class __c; }
    public static class StoreClientListSpec { public class __c; }
    public static class StoreClientListSurplus { public class __c; }
    public static class StoreClientListEvent { public class __c; }
}
