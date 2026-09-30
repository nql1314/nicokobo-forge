using System.Collections.ObjectModel;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nicokobo.Forge;

// No global window hook. Every delegate is retained with its owning window.
internal static class ForgeMachineUi
{
    private sealed class Binding(ForgeMachineInventory inventory)
    {
        internal ForgeMachineInventory Inventory { get; } = inventory;
        internal List<Delegate> Managed { get; } = [];
        internal List<Il2CppSystem.Func<GameItem, GameInventory, bool>> Native { get; } = [];
    }
    private static readonly Dictionary<IntPtr, Binding> Windows = new();
    internal static void Reset() => Windows.Clear();

    internal static GameItem Create(ForgeMachineDefinition definition)
    {
        if (!ForgeMachineRuntimeApi.RuntimeInstalled)
            throw new InvalidOperationException("Machine template runtime unavailable");
        GameItem? machine = null;
        try
        {
            machine = MachineFurnace.Furnace();
            if (machine == null || machine.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native furnace template unavailable");
            machine.identifier = definition.MachineId;
            machine.onCycleEndEarlySlotItemFunc = null;
            machine.onCycleEndSlotItemFunc = null;
            machine.onCycleEndLateSlotItemFunc = null;
            machine.forceDisableActivate = false;
            var window = Window(machine);
            var oldLayout = window.childElement.Cast<GridPixelElement>();
            var labels = Enumerable.Range(0, 5).Select(x => oldLayout.GetElement(x, 0)).ToArray();
            var oldBattery = oldLayout.GetElement(0, 1).Cast<GameSlotInventory>();
            var oldModules = oldLayout.GetElement(1, 1).Cast<GameGridInventory>();
            var oldInput = oldLayout.GetElement(2, 1).Cast<GameGridInventory>();
            var oldManual = oldLayout.GetElement(4, 1).Cast<GameSlotInventory>();
            var t = definition.Template;
            var layout = new GridPixelElement(5 + t.LiquidInputs.Count, 2, true).SetSpacing(4, 4);
            if (!window.Detach()) throw new InvalidOperationException("Template layout detach failed");
            oldLayout.DetachAll();

            GameSlotInventory? battery = null;
            if (t.Battery != null)
            {
                battery = oldBattery;
                battery.defaultGridWidth = t.Battery.Size.Width;
                battery.defaultGridHeight = t.Battery.Size.Height;
                battery.ResizePixels(t.Battery.Size.Width * 20, t.Battery.Size.Height * 20);
                Attach(layout, labels[0], 0, 0); Attach(layout, battery, 0, 1);
            }
            GameGridInventory? modules = null;
            if (t.Modules != null)
            {
                modules = oldModules.SetShape(t.Modules.Size.Width, t.Modules.Size.Height);
                Attach(layout, labels[1], 1, 0); Attach(layout, modules, 1, 1);
            }
            GameGridInventory? input = null;
            if (t.ItemInput != null)
            {
                input = oldInput.SetShape(t.ItemInput.Width, t.ItemInput.Height);
                Attach(layout, labels[2], 2, 0); Attach(layout, input, 2, 1);
            }
            GameInventory output = t.Output.Kind == ForgeMachineOutputKind.Items
                ? new GameGridInventory(t.Output.Size.Width, t.Output.Size.Height)
                : new GameSlotInventory(t.Output.Size.Width, t.Output.Size.Height)
                    .SetTooltipName("容器输出 / Container output");
            Attach(layout, labels[3], 3, 0); Attach(layout, output.Cast<PixelElement>(), 3, 1);
            GameSlotInventory? manual = null;
            if (t.ManualSlot)
            {
                manual = oldManual;
                Attach(layout, labels[4], 4, 0); Attach(layout, manual, 4, 1);
                MachineHelper.SetupManualSlot(manual);
            }
            var liquids = new Dictionary<string, GameSlotInventory>(StringComparer.Ordinal);
            for (int index = 0; index < t.LiquidInputs.Count; index++)
            {
                var slot = t.LiquidInputs[index];
                var inventory = new GameSlotInventory(slot.Size.Width, slot.Size.Height)
                    .SetTooltipName(slot.Label);
                Attach(layout, new RichTextElement().SetText(slot.Label), 5 + index, 0);
                Attach(layout, inventory, 5 + index, 1);
                liquids.Add(slot.SlotId, inventory);
            }
            if (!window.Attach(layout.Cast<PixelElement>())) throw new InvalidOperationException("Template layout attach failed");
            if (battery != null) MachineHelper.SetupBatterySlot(battery, machine);
            if (modules != null) MachineHelper.SetupModuleBay(modules, machine, null,
                new Il2CppStringArray(t.Modules!.AllowedTypes.ToArray()));
            var inventories = new ForgeMachineInventory(battery, modules, input, output,
                new ReadOnlyDictionary<string, GameSlotInventory>(liquids), manual);
            Bind(machine, definition.Template, inventories);
            definition.ConfigureItem?.Invoke(machine);
            if (machine.identifier != definition.MachineId || machine.unitCount != 1 ||
                Window(machine).Pointer != window.Pointer || machine.onCycleEndEarlySlotItemFunc != null ||
                machine.onCycleEndSlotItemFunc != null || machine.onCycleEndLateSlotItemFunc != null)
                throw new InvalidOperationException("Content configuration changed the machine contract");
            window.SetTitle(machine.name);
            window.Validate();
            if ((battery != null && MachineHelper.GetBatterySlot(machine)?.Pointer != battery.Pointer) ||
                (modules != null && MachineHelper.GetModuleInv(machine)?.Pointer != modules.Pointer))
                throw new InvalidOperationException("Native machine slot positions changed");
            return machine;
        }
        catch
        {
            if (machine != null && machine.Pointer != IntPtr.Zero)
            {
                try { Windows.Remove(Window(machine).Pointer); machine.Destroy(); }
                catch { /* Keep the factory failure. */ }
            }
            throw;
        }
    }

    private static void Attach(GridPixelElement layout, Il2CppObjectBase element, int x, int y)
    {
        if (element == null || !layout.Attach(element.Cast<PixelElement>(), x, y))
            throw new InvalidOperationException($"Template cell {x},{y} attach failed");
    }
    private static PixelWindow Window(GameItem machine)
    {
        if (machine.children is not { Count: 1 })
            throw new InvalidOperationException("Machine window missing");
        return machine.children[0].Cast<PixelWindow>();
    }

    internal static bool TryGet(GameItem machine, out ForgeMachineInventory? inventory)
    {
        inventory = null;
        if (machine == null || machine.Pointer == IntPtr.Zero ||
            !ForgeMachineRegistrationApi.TryGet(machine.identifier, out var profile) || profile == null) return false;
        try
        {
            var window = Window(machine);
            if (Windows.TryGetValue(window.Pointer, out var binding))
            { inventory = binding.Inventory; return true; }
            var layout = window.childElement.Cast<GridPixelElement>();
            var t = profile.Template;
            var liquids = new Dictionary<string, GameSlotInventory>(StringComparer.Ordinal);
            if (!profile.NativeMachine)
                for (int index = 0; index < t.LiquidInputs.Count; index++)
                    liquids.Add(t.LiquidInputs[index].SlotId,
                        layout.GetElement(5 + index, 1).Cast<GameSlotInventory>());
            inventory = new(t.Battery == null ? null : layout.GetElement(0, 1).Cast<GameSlotInventory>(),
                t.Modules == null ? null : layout.GetElement(1, 1).Cast<GameInventory>(),
                t.ItemInput == null ? null : layout.GetElement(2, 1).Cast<GameInventory>(),
                layout.GetElement(3, 1).Cast<GameInventory>(),
                new ReadOnlyDictionary<string, GameSlotInventory>(liquids),
                t.ManualSlot ? layout.GetElement(4, 1).Cast<GameSlotInventory>() : null);
            if (profile.NativeMachine) Windows[window.Pointer] = new(inventory);
            else Bind(machine, t, inventory);
            return true;
        }
        catch (Exception ex)
        {
            inventory = null;
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] slots unavailable: {ex.Message}");
            return false;
        }
    }

    private static void Bind(GameItem machine, ForgeMachineTemplate template, ForgeMachineInventory inventory)
    {
        var binding = new Binding(inventory);
        var updates = new List<(GameInventory Slot,
            Il2CppSystem.Func<GameItem, GameInventory, bool>? Prior,
            Il2CppSystem.Func<GameItem, GameInventory, bool> Next)>();
        void Admission(GameInventory slot, Func<GameItem, bool> accepts)
        {
            Func<GameItem, GameInventory, bool> managed = (item, _) =>
            {
                try { return item != null && item.Pointer != IntPtr.Zero && accepts(item); }
                catch { return false; }
            };
            var native = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem, GameInventory, bool>>(managed)
                ?? throw new InvalidOperationException("Inventory delegate conversion failed");
            native.Invoke(machine, slot); // Verify the bridge before publishing it.
            binding.Managed.Add(managed); binding.Native.Add(native);
            updates.Add((slot, slot.mayInventoryAddItemFunc, native));
        }
        if (inventory.ItemInput != null)
            Admission(inventory.ItemInput, item => ForgeMachineRegistrationApi.TryGet(machine.identifier, out var current) &&
                current != null && current.Accepts(item.identifier));
        foreach (var slot in template.LiquidInputs)
            Admission(inventory.LiquidInputs[slot.SlotId], item =>
                ForgeLiquidApi.IsContainer(item) && (slot.Condition?.Invoke(item) ?? true));
        if (template.Output.Kind == ForgeMachineOutputKind.Container)
            Admission(inventory.Output, item => ForgeLiquidApi.IsContainer(item) &&
                (template.Output.ContainerCondition?.Invoke(item) ?? true));
        try
        {
            foreach (var update in updates) update.Slot.mayInventoryAddItemFunc = update.Next;
            Windows[Window(machine).Pointer] = binding;
        }
        catch
        {
            foreach (var update in updates) update.Slot.mayInventoryAddItemFunc = update.Prior;
            throw;
        }
    }
}
