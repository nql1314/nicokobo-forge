using System.Reflection;
using Il2Cpp;

namespace Il2Cpp
{
    public sealed class GameItem
    {
        public IntPtr Pointer = (IntPtr)42;
        public string identifier = "lifecycle-machine";
        public int unitCount = 1;
        public long unitValue;
        public GameInventory? parentInventory;
        public ItemState state = new();
        public ItemState? modifiedState;
        public int GetUniqueID() => (int)Pointer;
        public bool IsTag(string tag) => (modifiedState ?? state).dict.GetValueOrDefault(tag)?.enabled == true;
        public TagState? GetTagReadonly(string tag) => (modifiedState ?? state).dict.GetValueOrDefault(tag)?.Clone();
        public void SyncModifiedState() { }
        public SlotMarker? TryFindOneValidInventorySlot(GameItem item) => null;
    }
    public sealed class ItemState { public Dictionary<string, TagState> dict = []; }
    public sealed class TagState(string id, string name)
    {
        public IntPtr Pointer = (IntPtr)44;
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string valueString = "";
        public bool enabled;
        public TagState Clone() => new(Id, Name) { valueString = valueString, enabled = enabled };
        public void Enable() => enabled = true;
        public void SetString(string value) => valueString = value;
    }
    public class PixelElement
    { public T Cast<T>() => (T)(object)this; }
    public class GameInventory : PixelElement
    {
        public IntPtr Pointer = (IntPtr)43;
        public List<GameItem> childItems = [];
    }
    public sealed class GameSlotInventory : GameInventory { public GameItem? childItem; }
    public sealed class SlotMarker { }
    public sealed class PixelWindow : PixelElement
    { public bool Detach() => true; public bool Attach(PixelElement child) => true; }
    public sealed class GridPixelElement : PixelElement
    { public PixelElement GetElement(int x, int y) => new(); }
    public static class MachineFurnace { public static GameItem Furnace() => new(); }
    public static class ToolDirectory
    {
        public sealed class __c__DisplayClass23_0 { public void _CreateTurboBooster_b__4(GameItem a, GameItem b) { } }
        public sealed class __c__DisplayClass24_0 { public void _CreateTurboBoosterAdv_b__7(GameItem a, GameItem b) { } }
    }
    public static class WaterHelper
    { public static void EmptyContainer(GameItem item) { } public static void AddLiquid(GameItem item, string id, int amount) { } }
    public static class PowerHelper { public static bool DrawPowerSource(GameItem item, int cost) => true; }
    public static class GeneralHelper { public static bool IsItemOwned(GameItem item) => true; }
    public sealed class PlayerStore
    {
        public static PlayerStore Instance { get; set; } = new();
        public static PlayerStore instance => Instance;
        public IntPtr Pointer { get; } = (IntPtr)41;
        public string runID = "lifecycle-run";
        public int saveSlotId = 2;
        public int BodyCalls;
        public List<GameItem> Items = [];
        public void LoadGame() => BodyCalls++;
        public void EndNight() => BodyCalls++;
        public void EndDay() => BodyCalls++;
    }
    public static class StoreStation { public static int GetDayCounter() => 7; }
    public static class ModHook
    {
        public static int BodyCalls;
        public static void FireOnHandlingNightlyServicesEarly() => BodyCalls++;
        public static void FireOnHandlingNightlyServicesLate() => BodyCalls++;
        public static void FireOnGoingSleepLate() => BodyCalls++;
        public static void FireOnGameLoadedLate() => BodyCalls++;
    }
}

namespace Nicokobo.Forge
{
    public static class ForgeInventoryApi
    {
        public static IReadOnlyList<GameItem> CaptureRunItems(PlayerStore store) => store.Items;
    }
}

namespace Nicokobo.Forge.Runtime
{
    internal sealed record NativeHook(Type TargetType, string Method, Type[] Arguments,
        Type ReturnType, Type CallbackType, string? Prefix = null,
        string? Postfix = null, string? Finalizer = null);
    internal static class NativeHookSet
    {
        internal static readonly Dictionary<string, IReadOnlyList<NativeHook>> Installed = [];
        internal static bool Install(string id, IReadOnlyList<NativeHook> hooks, Action<string>? log)
        {
            foreach (var hook in hooks)
                if (hook.TargetType.GetMethod(hook.Method) == null) return false;
            Installed[id] = hooks;
            return true;
        }
        internal static void Remove(string id, Action<string>? log) => Installed.Remove(id);
    }

    // Model the Harmony native boundary, including a foreign prefix rejecting
    // the original. Production dispatch, subscription and callbacks are linked.
    internal static class Boundary
    {
        internal static void Call(Type type, string name, PlayerStore store, bool runOriginal)
        {
            var target = type.GetMethod(name) ?? throw new MissingMethodException(name);
            var hooks = NativeHookSet.Installed.Values.SelectMany(x => x)
                .Where(h => h.TargetType == type && h.Method == name).ToArray();
            foreach (var hook in hooks) Invoke(hook, hook.Prefix, store, runOriginal);
            if (runOriginal) target.Invoke(target.IsStatic ? null : store, null);
            foreach (var hook in hooks) Invoke(hook, hook.Postfix, store, runOriginal);
        }
        private static void Invoke(NativeHook hook, string? callback, PlayerStore store, bool runOriginal)
        {
            if (callback == null) return;
            var method = hook.CallbackType.GetMethod(callback, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(callback);
            var args = method.GetParameters().Select(p => p.ParameterType == typeof(PlayerStore)
                ? (object)store : p.ParameterType == typeof(bool) && p.Name == "__runOriginal" ? runOriginal
                : throw new InvalidOperationException("Unexpected hook parameter: " + p.Name)).ToArray();
            method.Invoke(null, args);
        }
    }
}
