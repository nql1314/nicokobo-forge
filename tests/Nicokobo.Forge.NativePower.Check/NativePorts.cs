using System.Reflection;
using Nicokobo.Forge;

// This executable links the production adapter. These ports model the native
// tree/slot boundary and call its actual Harmony callbacks without loading a
// game, installing detours, or claiming IL2CPP execution.
namespace HarmonyLib { internal sealed class HarmonyPriority : Attribute { internal HarmonyPriority(int priority) { } } }
namespace UnityEngine
{
    public readonly record struct Vector3(float X, float Y, float Z);
    public sealed class Transform { public Vector3 localPosition; }
}
namespace Nicokobo.Forge.Runtime
{
    internal sealed record NativeHook(Type TargetType, string Method, Type[] Arguments, Type ReturnType, Type CallbackType,
        string? Prefix = null, string? Postfix = null, string? Finalizer = null);
    internal static class NativeHookSet
    {
        internal static string? FailTarget;
        internal static bool Install(string id, IReadOnlyList<NativeHook> hooks, Action<string> log) =>
            !hooks.Any(hook => hook.Method == FailTarget);
    }
    internal static class OwnedCallbacks<T, U> { internal static bool ValidId(string owner, string id) => owner.Length > 0 && id.Length > 0; }
    internal sealed class CallbackLease(Action release) : IDisposable { public void Dispose() => release(); }
}
namespace Nicokobo.Forge
{
    public interface IForgePowerTransaction : IDisposable
    { bool Validate(); void Debit(); bool Verify(); bool Restore(); void Fault(string reason); }
    internal static class PowerConnectorIdentity
    { internal static T? Find<T>(Dictionary<string, T> entries, string id) where T : class => entries.GetValueOrDefault(id); }
    internal static class ForgeInventoryFreezeApi { internal static bool IsFrozen(Il2Cpp.GameItem item) => false; }
    internal static class ForgeNumbers { internal static class Inventory { internal const int MaxRunItems = 8192; } }
    internal sealed class ForgeCapabilities
    {
        internal static ForgeCapabilities Current = new();
        internal bool KnownGameBuild = true;
    }
    public sealed record ForgeMachineInventory(Il2Cpp.GameSlotInventory? Battery);
    internal static class ForgeMachineRegistrationApi
    {
        internal static readonly HashSet<string> Registered = [];
        internal static bool TryGet(string id, out object? profile)
        { profile = Registered.Contains(id) ? new() : null; return profile != null; }
    }
    internal static class ForgeMachineRuntimeApi
    {
        internal static bool TryGetInventory(Il2Cpp.GameItem machine, out ForgeMachineInventory? inventory)
        { inventory = machine.RegisteredInventory; return inventory != null; }
    }
}
namespace Il2Cpp
{
    public class PixelElement
    {
        private static long _next;
        public IntPtr Pointer { get; } = new(++_next);
        public T? TryCast<T>() where T : class => this as T;
    }
    public sealed class PixelWindow(PixelElement child) : PixelElement { public PixelElement childElement = child; }
    public sealed class GridPixelElement(int width, int height) : PixelElement
    {
        public int gridWidth = width, gridHeight = height;
        private readonly Dictionary<(int X, int Y), PixelElement> _cells = [];
        public PixelElement? GetElement(int x, int y) => _cells.GetValueOrDefault((x, y));
        public void Put(int x, int y, PixelElement element) => _cells[(x, y)] = element;
    }
    public sealed class GridShape
    {
        public int minX, minY, width = 1, height = 1, globalWidth = 1, globalHeight = 1, orientation;
        public bool flipped;
        public GridShape Clone() => (GridShape)MemberwiseClone();
    }
    public sealed class ItemFeature : PixelElement;
    public sealed class TagState
    {
        public bool valueEnabled, valueBool;
        public string valueString = "";
        public int valueInt;
        public long valueLong;
        public float valueFloat;
        public double valueDouble;
        public TagState Enable() { valueEnabled = true; return this; }
        public TagState SetString(string value) { valueString = value; return this; }
        public TagState Clone() => (TagState)MemberwiseClone();
    }
    public sealed class TagSystem
    {
        public Dictionary<string, TagState> dict = [];
        public TagState GetTag(string id)
        { if (!dict.TryGetValue(id, out var tag)) dict[id] = tag = new(); return tag; }
        public TagSystem Clone() => new() { dict = dict.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()) };
    }
    public class GameItem : PixelElement
    {
        private static int _nextId;
        public int Id = ++_nextId, unitCount = 1, Cost = 4, Energy;
        public string identifier = "item", spriteAtlasPath = "original-atlas", spritePath = "original-sprite";
        public bool Owned = true, Destroyed, spriteChanged;
        public long unitValue = 10;
        public TagSystem state = new();
        public GridShape modifiedShape = new();
        public List<ItemFeature> itemFeatures = [];
        public List<PixelElement> children = [];
        public GameInventory? parentInventory;
        public Func<bool>? mayInventoryRemoveItemFunc;
        public Action? onSlotAddItemFunc;
        public ForgeMachineInventory? RegisteredInventory;
        public PixelWindow? contentWindow => children.OfType<PixelWindow>().FirstOrDefault();
        public TagState? GetTagReadonly(string id) => state.dict.GetValueOrDefault(id);
        public int GetUniqueID() => Id;
        public void SyncModifiedState() { }
        public void SetUnitCount(int count) => unitCount = count;
        public void SetSpriteAndShape(string atlas, string path) { spriteAtlasPath = atlas; spritePath = path; spriteChanged = true; }
        public SlotMarker? GetIncompleteSlotInInventory() => parentInventory == null ? null :
            new() { item = this, inventory = parentInventory, itemGridShape = modifiedShape.Clone(), index = parentInventory.childItems.IndexOf(this), numTransfer = unitCount };
        public virtual void Destroy()
        {
            if (!(bool)Callback.Invoke("DeferConsumedDestroy", [this])!) return;
            parentInventory?.Expel(this); Destroyed = true;
        }
    }
    public sealed class RenderHandler : PixelElement { public UnityEngine.Transform transform = new(); }
    public sealed class GameItemElement : GameItem { public RenderHandler handler = new(); }
    public class GameInventory : PixelElement
    {
        protected readonly List<GameItem> Items = [];
        public virtual List<GameItem> childItems => Items;
        public GameItem? Owner;
        public bool overrideLockInsert, overrideLockRemove;
        public Func<GameItem, bool>? mayInventoryAddItemFunc;
        public Action? onSlotAddItemFunc, onSlotAddItemEndFunc, onSlotAddItemLateFunc;
        public GameItem GetParentItem() => Owner!;
        public virtual bool UncheckedAccept(GameItem item)
        {
            if (item.parentInventory != null) return false;
            Items.Add(item); item.parentInventory = this; return true;
        }
        public virtual bool Expel(GameItem item)
        { if (!Items.Remove(item)) return false; item.parentInventory = null; return true; }
        public SlotMarker TryInventorySlot(GameItem item, int count, GridShape shape) => new()
        { item = item, inventory = this, itemGridShape = shape, index = Items.Count, numTransfer = count };
    }
    public sealed class GameGridInventory : GameInventory;
    public sealed class GameSlotInventory : GameInventory
    {
        public GameItem? currentItem
        {
            get => Items.FirstOrDefault();
            set { Items.Clear(); if (value != null) Items.Add(value); }
        }
        public GameItem? childItem => currentItem;
        public override bool UncheckedAccept(GameItem item) => Items.Count == 0 && base.UncheckedAccept(item);
    }
    public sealed class SlotMarker
    {
        public GameItem? item, targetItem;
        public GameInventory? inventory;
        public GridShape? itemGridShape;
        public int index, numTransfer;
        public bool IsValid() => true;
    }
    public sealed class RichTextBuilder { public void AddLine(string line) { } }
    public sealed class PlayerStore : PixelElement
    {
        public static PlayerStore? instance;
        public int projectorAttractivenessBonus;
        public bool isSecurityAlarmActive;
        public ClientInstance? currentClientInstance;
    }
    public sealed class ClientInstance : PixelElement { public StoreClient storeClient = new(); }
    public sealed class StoreClient : PixelElement { public bool isAugScanned; }
    public static class GeneralHelper { public static bool IsItemOwned(GameItem machine) => machine.Owned; }
    public static class PowerHelper
    {
        public static int GetAvailableEnergyFromItem(GameItem source, bool ignored)
        { int value = 0; return Callback.Prefix("ReadEnergy", [source], ref value) ? source.Energy : value; }
        public static bool CanDrawPowerSource(GameItem source, int cost)
        {
            bool value = false;
            if (!Callback.Prefix("CanDrawToolQuery", [source, cost], ref value)) return value;
            return Callback.Prefix("CanDraw", [source, cost], ref value) ? source.Energy >= cost : value;
        }
        public static bool DrawPowerSource(GameItem source, int cost)
        {
            bool value = false;
            if (!Callback.Prefix("Draw", [source, cost], ref value)) return value;
            if (source.Energy < cost) return false;
            source.Energy -= cost; return true;
        }
        public static void ChargeBattery(GameItem item) { }
        public static void ChargeBattery(GameItem item, int cost, bool ignoreBlackout) { }
        public static void SetPowerSourceAt(GameItem item, int value) => item.Energy = value;
        public static void InitPowerSourceItem(GameItem item, int current, int maximum) { }
        public static bool IsPowerSourceFull(GameItem item) => true;
        public static void CreatePowerSourceItemTooltip(RichTextBuilder builder, GameItem item) { }
        public static int TryRemoveEnergy(GameItem item, int cost, bool ignored)
        { int removed = Math.Min(item.Energy, cost); item.Energy -= removed; return removed; }
        public static void UpdateEnergyCreditSprite(GameItem item) { }
    }
    public static class MachineryHelper { public static int GetMachinePowerUsage(GameItem item) => item.Cost; }
    public static class MachineHelper
    {
        public static bool CanPower(GameItem item) => true;
        public static bool CanPower(GameItem item, GameSlotInventory slot) => true;
        public static bool HasBattery(GameItem item) => true;
        public static bool TryDrawCyclePower(GameItem machine, GameSlotInventory slot)
        { bool value = false; return Callback.Prefix("DrawMachine", [machine], ref value) ? PowerHelper.DrawPowerSource(slot.childItem!, machine.Cost) : value; }
    }
    public static class MachineFurnace { public sealed class __c__DisplayClass0_0 { public GameItem furnace = null!; public GameSlotInventory itemInventoryBattery = null!; } }
    public static class MachinePurifier { public sealed class __c__DisplayClass1_0 { public GameItem machine = null!; public GameSlotInventory powerInv = null!; } }
    public static class MachineMoistureFarm { public sealed class __c__DisplayClass0_0 { public GameItem machine = null!; public GameSlotInventory powerInv = null!; } }
    public static class MachineDesequencer { public sealed class __c__DisplayClass5_0 { public GameItem machine = null!; public GameSlotInventory powerInv = null!; } }
    public static class MachineWineRack { public sealed class __c__DisplayClass3_0 { public GameItem machine = null!; public GameSlotInventory itemInventoryBattery = null!; } }
    public static class MachineBottlePrinter { public sealed class __c__DisplayClass6_0 { public GameItem machine = null!; public GameSlotInventory itemInventory = null!; } }
    public static class MachineBoxDispenser { public sealed class __c__DisplayClass0_0 { public GameSlotInventory itemInventory = null!; } }
    public static class MachineFridge { public sealed class __c__DisplayClass26_0 { public GameItem machine = null!; public GameSlotInventory batterySlot = null!; } }
    public static class MachineProjector { public sealed class __c__DisplayClass1_0 { public GameItem machine = null!; public GameSlotInventory itemInventory = null!; } }
    public static class MachineAlarm { public sealed class __c__DisplayClass1_0 { public GameItem machine = null!; public GameSlotInventory itemInventory = null!; } }
    public static class ToolDirectory
    {
        public sealed class __c__DisplayClass24_0 { public GameItem item = null!; }
        public sealed class __c__DisplayClass25_0 { public GameItem item = null!; public GameSlotInventory batterySlot = null!; }
    }
    public static class MiscItemDirectory { public sealed class __c__DisplayClass75_0 { public GameSlotInventory batterySlot = null!; } }
    public static class AugScanner
    {
        public static void CreateTooltip(RichTextBuilder builder, GameItem item) { }
        public static GameSlotInventory GetBatterySlot(GameItem item) => item.contentWindow!.childElement.TryCast<GridPixelElement>()!.GetElement(0, 1)!.TryCast<GameSlotInventory>()!;
    }
    public static class MachineTurboBoosterAdv { public static void UpdateSprite(GameItem item, GameSlotInventory slot) { } }
    public static class HusbandryDirectory
    {
        public sealed class __c__DisplayClass1_0
        {
            public GameItem item = null!;
            public GameSlotInventory batterySlot = null!;
            public Action<GameInventory>? Cleanup;
            public Action? Incinerate;
            public bool Method_Internal_Boolean_GameInventory_PDM_0(GameInventory inventory)
            { Callback.Native("BeforeVacuumCleanup", this, () => Cleanup?.Invoke(inventory), inventory); return true; }
            public void Method_Internal_Void_PDM_0() => Callback.Native("BeforeVacuumIncinerate", this, () => Incinerate?.Invoke());
        }
    }
}

internal static class Callback
{
    private static readonly Type Adapter = typeof(ForgeExternalPowerApi);
    internal static object? Invoke(string method, object?[] args) => Adapter.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    internal static bool Prefix<T>(string method, object?[] values, ref T result)
    {
        var callback = Adapter.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = values.Concat(new object?[] { result }).ToArray();
        bool run = (bool)callback.Invoke(null, args)!;
        result = (T)args[^1]!; return run;
    }
    internal static void Native(string prefix, object instance, Action body, object? arg0 = null, object? arg1 = null)
    {
        var method = Adapter.GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = method.GetParameters().Select(parameter => parameter.Name switch
        { "__instance" => instance, "__0" => arg0, "__1" => arg1, _ => null }).ToArray();
        bool run = (bool)method.Invoke(null, args)!;
        object? state = args[^1];
        try { if (run) body(); }
        catch (Exception exception)
        { Invoke("FinishNativeBatch", [exception, state]); throw; }
        Invoke("AfterNativeBatch", [state, run]);
        Invoke("FinishNativeBatch", [null, state]);
    }
    internal static void Query(string prefix, Action body, object? arg0 = null, object? arg1 = null)
    {
        var method = Adapter.GetMethod(prefix, BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = method.GetParameters().Select(parameter => parameter.Name switch
        { "__0" => arg0, "__1" => arg1, _ => null }).ToArray();
        method.Invoke(null, args);
        object? state = args[^1];
        try { body(); }
        catch (Exception exception) { Invoke("FinishToolPowerQuery", [exception, state]); throw; }
        Invoke("AfterToolPowerQuery", [state]);
        Invoke("FinishToolPowerQuery", [null, state]);
    }
}
