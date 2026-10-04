using System.Reflection;
using Nicokobo.Forge.Registration;

// Offline placement fixtures run the production adapter's installed callbacks.
// They model shelf capacity and native rejection cleanup, without IL2CPP or Harmony.
namespace Nicokobo.Forge.Runtime
{
    internal sealed record NativeHook(Type TargetType, string Method, Type[] Arguments,
        Type ReturnType, Type CallbackType, string? Prefix = null,
        string? Postfix = null, string? Finalizer = null);

    internal static class NativeHookSet
    {
        internal static IReadOnlyList<NativeHook> Installed = [];
        internal static bool Install(string id, IReadOnlyList<NativeHook> hooks, Action<string>? log)
        { Installed = hooks; return true; }
        internal static void Postfix(Type type, string method, params object[] arguments)
        {
            foreach (var hook in Installed.Where(h => h.TargetType == type && h.Method == method))
                if (hook.Postfix is { } callback)
                    hook.CallbackType.GetMethod(callback, BindingFlags.Static | BindingFlags.NonPublic)!
                        .Invoke(null, arguments);
        }
    }
}

namespace Nicokobo.Forge
{
    internal static class NativeItemRegistry
    {
        internal static readonly List<NativeItemDeclaration> Offers = [];
        internal static readonly Dictionary<string, NativeApplicationStatus> Outcomes = [];
        internal static IReadOnlyList<NativeItemDeclaration> Declarations() => Offers.ToArray();
        internal static NativeApplicationStatus Outcome(string id) =>
            Outcomes.GetValueOrDefault(id, NativeApplicationStatus.Applied);
    }
}

namespace Il2Cpp
{
    public sealed partial class GameItem
    {
        public string identifier = "";
        public int unitCount = 1, ShelfSpace = 1, DestroyCalls;
        public bool Owned;
        public PixelElement? parentInventory;
        public void Destroy()
        {
            if (++DestroyCalls > 1) throw new InvalidOperationException("Item destroyed twice");
            if (parentInventory is GameGridInventory inventory) inventory.childItems.Remove(this);
            parentInventory = null;
        }
    }

    public sealed partial class GameGridInventory
    {
        public List<GameItem> childItems = [];
        public int Capacity = 100, SlotUnits = int.MaxValue;
        public bool ThrowOnPreflight;
        public SlotMarker? TryFindOneValidInventorySlot(GameItem item, bool keepOrientation = false)
        {
            if (ThrowOnPreflight) throw new InvalidOperationException("Slot lookup failed");
            if (item.Owned || item.ShelfSpace > Capacity - childItems.Sum(x => x.ShelfSpace)) return null;
            return new() { numTransfer = Math.Min(SlotUnits, item.unitCount) };
        }
    }

    public sealed class SlotMarker
    {
        public IntPtr Pointer = new(1);
        public int numTransfer;
        public bool IsValid() => numTransfer > 0;
    }

    public sealed class PlayerStore
    {
        public static PlayerStore? instance;
        public static int SingletonCreations;
        public static PlayerStore Instance { get { SingletonCreations++; return instance ??= new(); } }
        public IntPtr Pointer = new(1);
        public HashSet<string> OwnedIds = [];
        public int Placements;
        public bool RejectPlacement, ThrowAfterRejection;
        public bool IsPlayerOwnThisItem(string id) => OwnedIds.Contains(id);
        public void AddDirectSellingItemToTable(GameItem item, bool owned, bool stolen,
            bool ignorePerk, int heat)
        {
            Placements++;
            if (RejectPlacement)
            {
                item.Destroy();
                if (ThrowAfterRejection) throw new InvalidOperationException("Rejected by native placement");
                return;
            }
            var inventory = EmporiumEntry.Instance!.frontInvinvElement;
            item.parentInventory = inventory;
            inventory.childItems.Add(item);
        }
        public void OnItemBought(GameItem item, int cost)
        {
            GeneralHelper.SetItemOwned(item, true);
            OwnedIds.Add(item.identifier);
            if (item.parentInventory is GameGridInventory inventory) inventory.childItems.Remove(item);
            item.parentInventory = null;
            Nicokobo.Forge.Runtime.NativeHookSet.Postfix(typeof(PlayerStore), nameof(OnItemBought), item);
        }
    }

    public sealed class EmporiumEntry
    {
        public static EmporiumEntry? Instance;
        public GameGridInventory frontInvinvElement = new();
    }

    public static class GeneralHelper
    {
        public static void SetItemOwned(GameItem item, bool owned = true) => item.Owned = owned;
    }

    public static class DirectoryMaster
    {
        public static GameItem Item(string id, bool clone) =>
            ((Func<GameItem>)Nicokobo.Forge.NativeItemRegistry.Offers.Single(x => x.ItemId == id).Factory)();
    }

    public static class StoreClientList
    {
        public static void PlaceInventorInventory(bool isVisitingPlayerStore = false) =>
            Nicokobo.Forge.Runtime.NativeHookSet.Postfix(typeof(StoreClientList),
                nameof(PlaceInventorInventory), isVisitingPlayerStore);
    }
}
