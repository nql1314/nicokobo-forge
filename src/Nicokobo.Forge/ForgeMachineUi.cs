using System.Collections.ObjectModel;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

// No global window hook. Admission is a native whitelist on each live slot; the
// window pointer only maps back to the inventories attached to it.
internal static class ForgeMachineUi
{
    /// <summary>Native liquid-container marker. MachinePurifier and the moisture
    /// farm whitelist their water slots with the same tag.</summary>
    private const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";
    private static string ContainerLabel() =>
        LocHelper.GetLocalizedMechanic("mech_purifier_label_container",
            (Il2CppReferenceArray<Il2CppSystem.Object>?)null);

    private sealed class Binding(MachineProfile profile, ForgeMachineInventory inventory)
    {
        internal MachineProfile Profile { get; } = profile;
        internal ForgeMachineInventory Inventory { get; } = inventory;
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
            var oldOutput = oldLayout.GetElement(3, 1).Cast<GameGridInventory>();
            var oldManual = oldLayout.GetElement(4, 1).Cast<GameSlotInventory>();
            var t = definition.Template;
            var layout = new GridPixelElement(5 + t.LiquidInputs.Count, 2, true).SetSpacing(ForgeNumbers.Machines.LayoutSpacing, ForgeNumbers.Machines.LayoutSpacing);
            if (!window.Detach()) throw new InvalidOperationException("Template layout detach failed");
            oldLayout.DetachAll();

            GameSlotInventory? battery = null;
            if (t.Battery != null)
            {
                battery = t.Battery.Size.Width == oldBattery.defaultGridWidth &&
                    t.Battery.Size.Height == oldBattery.defaultGridHeight
                    ? oldBattery : CreateSlot(t.Battery.Size);
                Attach(layout, labels[0], 0, 0); Attach(layout, battery, 0, 1);
            }
            GameGridInventory? modules = null;
            if (t.Modules != null)
            {
                // Keep the native ring intact. A custom rectangle needs a fresh
                // inventory, without the ring's already-rendered background.
                modules = GridMatches(oldModules, t.Modules.Size)
                    ? oldModules : new GameGridInventory(t.Modules.Size.Width, t.Modules.Size.Height);
                Attach(layout, labels[1], 1, 0); Attach(layout, modules, 1, 1);
            }
            GameGridInventory? input = null;
            if (t.ItemInput != null)
            {
                input = GridMatches(oldInput, t.ItemInput)
                    ? oldInput : new GameGridInventory(t.ItemInput.Width, t.ItemInput.Height);
                Attach(layout, labels[2], 2, 0); Attach(layout, input, 2, 1);
            }
            GameInventory output = t.Output.Kind == ForgeMachineOutputKind.Items
                ? GridMatches(oldOutput, t.Output.Size) ? oldOutput
                    : new GameGridInventory(t.Output.Size.Width, t.Output.Size.Height)
                : CreateContainerSlot(t.Output.Size).SetTooltipName(ContainerLabel());
            if (t.Output.Kind == ForgeMachineOutputKind.Items)
                // Keep the native output-only predicate on fresh warehouses too.
                output.mayInventoryAddItemFunc = oldOutput.mayInventoryAddItemFunc;
            int outputColumn = OutputColumn(t);
            Attach(layout, labels[3], outputColumn, 0);
            Attach(layout, output.Cast<PixelElement>(), outputColumn, 1);
            GameSlotInventory? manual = null;
            if (t.ManualSlot)
            {
                manual = oldManual;
                Attach(layout, labels[4], outputColumn + 1, 0);
                Attach(layout, manual, outputColumn + 1, 1);
                MachineHelper.SetupManualSlot(manual);
            }
            var liquids = new Dictionary<string, GameSlotInventory>(StringComparer.Ordinal);
            for (int index = 0; index < t.LiquidInputs.Count; index++)
            {
                var slot = t.LiquidInputs[index];
                string label = string.IsNullOrWhiteSpace(slot.Label) ? ContainerLabel() : slot.Label;
                var inventory = CreateContainerSlot(slot.Size).SetTooltipName(label);
                Attach(layout, new TagElement().SetText(label), 3 + index, 0);
                Attach(layout, inventory, 3 + index, 1);
                liquids.Add(slot.SlotId, inventory);
            }
            if (!window.Attach(layout.Cast<PixelElement>())) throw new InvalidOperationException("Template layout attach failed");
            if (battery != null) MachineHelper.SetupBatterySlot(battery, machine);
            if (modules != null) MachineHelper.SetupModuleBay(modules, machine, null,
                new Il2CppStringArray(t.Modules!.AllowedTypes.ToArray()));
            var inventories = new ForgeMachineInventory(battery, modules, input, output,
                new ReadOnlyDictionary<string, GameSlotInventory>(liquids), manual);
            if (!ForgeMachineRegistrationApi.TryGet(definition.MachineId, out var profile) || profile == null)
                throw new InvalidOperationException("Machine profile missing");
            Bind(machine, profile, inventories);
            definition.ConfigureItem?.Invoke(machine);
            if (machine.identifier != definition.MachineId || machine.unitCount != 1 ||
                Window(machine).Pointer != window.Pointer || machine.onCycleEndEarlySlotItemFunc != null ||
                machine.onCycleEndSlotItemFunc != null || machine.onCycleEndLateSlotItemFunc != null)
                throw new InvalidOperationException("Content configuration changed the machine contract");
            window.SetTitle(machine.name);
            window.Validate();
            // MachineHelper.GetBatterySlot/GetModuleInv only read (0,1) and (1,1)
            // back from this window, so their answer can never move a slot we just
            // attached. A null answer is expected here: the machine has not been
            // through MachineHelper.OnLoad yet, which runs after the item factory
            // returns. Never fail the factory on that timing; keep the answer as
            // diagnostics only.
            WarnOnSlotReadback(machine, battery, modules);
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

    private static void WarnOnSlotReadback(GameItem machine, GameSlotInventory? battery,
        GameGridInventory? modules)
    {
        if (battery == null && modules == null) return;
        try
        {
            var nativeBattery = battery == null ? null : MachineHelper.GetBatterySlot(machine);
            var nativeModules = modules == null ? null : MachineHelper.GetModuleInv(machine);
            if (battery != null && nativeBattery != null && nativeBattery.Pointer != battery.Pointer)
                ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] {machine.identifier}: " +
                    $"battery slot readback 0x{nativeBattery.Pointer:X} differs from attached 0x{battery.Pointer:X}");
            if (modules != null && nativeModules != null && nativeModules.Pointer != modules.Pointer)
                ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] {machine.identifier}: " +
                    $"module bay readback 0x{nativeModules.Pointer:X} differs from attached 0x{modules.Pointer:X}");
        }
        catch (Exception ex)
        {
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] {machine.identifier}: " +
                $"native slot readback unavailable: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Attach(GridPixelElement layout, Il2CppObjectBase element, int x, int y)
    {
        // Attach(element, int, int) means minimumWidth/minimumHeight, not x/y.
        // AttachPos avoids that overload and preserves native helper addresses.
        if (element == null || !layout.AttachPos(element.Cast<PixelElement>(), x, y) ||
            layout.GetElement(x, y)?.Pointer != element.Pointer)
            throw new InvalidOperationException($"Template cell {x},{y} attach failed");
    }
    private static bool GridMatches(GameGridInventory inventory, ForgeMachineGrid size) =>
        inventory.inventoryShape.width == size.Width && inventory.inventoryShape.height == size.Height;
    private static GameSlotInventory CreateSlot(ForgeMachineGrid size) =>
        size.Width == 1 && size.Height == 1 ? new GameSlotInventory()
            : new GameSlotInventory(size.Width, size.Height);
    private static GameSlotInventory CreateContainerSlot(ForgeMachineGrid size) =>
        // Same constructor and placeholder as MachinePurifier.Purifier().
        // The native slot grows for a jug and shrinks again when it is removed.
        CreateSlot(size).SetBackgroundFadeSprite("Items/water_container4", "xl");
    private static int OutputColumn(ForgeMachineTemplate template) => 3 + template.LiquidInputs.Count;
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
            {
                inventory = binding.Inventory;
                if (!ReferenceEquals(binding.Profile, profile)) Bind(machine, profile, inventory);
                return true;
            }
            var layout = window.childElement.Cast<GridPixelElement>();
            var t = profile.Template;
            var liquids = new Dictionary<string, GameSlotInventory>(StringComparer.Ordinal);
            for (int index = 0; index < t.LiquidInputs.Count; index++)
                liquids.Add(t.LiquidInputs[index].SlotId,
                    layout.GetElement(3 + index, 1).Cast<GameSlotInventory>());
            inventory = new(t.Battery == null ? null : layout.GetElement(0, 1).Cast<GameSlotInventory>(),
                t.Modules == null ? null : layout.GetElement(1, 1).Cast<GameInventory>(),
                t.ItemInput == null ? null : layout.GetElement(2, 1).Cast<GameInventory>(),
                layout.GetElement(OutputColumn(t), 1).Cast<GameInventory>(),
                new ReadOnlyDictionary<string, GameSlotInventory>(liquids),
                t.ManualSlot ? layout.GetElement(OutputColumn(t) + 1, 1).Cast<GameSlotInventory>() : null);
            Bind(machine, profile, inventory);
            return true;
        }
        catch (Exception ex)
        {
            inventory = null;
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] slots unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>Install native admission whitelists. The game reads
    /// GameInventoryFunc.mayInventoryAddItemFunc through
    /// InvokeFuncExtensions.InvokeAllReduce, which invokes every entry with
    /// Delegate.DynamicInvoke. A delegate built by DelegateSupport.ConvertDelegate
    /// reports Func`3.Invoke with an Il2CppToMonoDelegateReference target, so that
    /// call throws TargetException: Object does not match target type. The
    /// ContainerHelper closures are compiled into il2cpp and survive it.</summary>
    private static void Bind(GameItem machine, MachineProfile profile,
        ForgeMachineInventory inventory)
    {
        if (inventory.ItemInput != null) AdmitItemInput(machine, profile, inventory.ItemInput);
        foreach (var slot in profile.Template.LiquidInputs)
            AdmitContainer(machine, inventory.LiquidInputs[slot.SlotId], "liquid-input:" + slot.SlotId);
        if (profile.Template.Output.Kind == ForgeMachineOutputKind.Container)
            AdmitContainer(machine, inventory.Output, "container-output");
        Windows[Window(machine).Pointer] = new(profile, inventory);
    }

    // The native GridPixelElement searches right-to-left, which makes a
    // container output win over a water input. Ask each native slot in machine
    // order; its own admission, capacity and stacking rules still decide.
    internal static SlotMarker? FindDropSlot(GameItem machine, GameItem item)
    {
        if (!TryGet(machine, out var inventory) || inventory == null) return null;
        ForgeMachineRegistrationApi.TryGet(machine.identifier, out var profile);
        bool material = profile != null && (profile.IsFeedstock(item.identifier) || profile.IsFeedstockTag(item.IsTag));
        IEnumerable<GameInventory?> slots = new GameInventory?[] { material ? inventory.ItemInput : null,
                inventory.Battery, inventory.Modules }
            .Concat(inventory.LiquidInputs.Values)
            .Concat(new GameInventory?[] { material ? null : inventory.ItemInput, inventory.Output, inventory.Manual });
        foreach (var inventorySlot in slots)
        {
            var slot = inventorySlot?.TryFindOneValidInventorySlot(item);
            if (slot?.IsValid() == true && slot.numTransfer > 0) return slot;
        }
        return null;
    }

    /// <summary>The item input admits the frozen recipe IDs. Shape, stacking and
    /// "is this a real batch input" stay with the native slot and the batch runtime,
    /// which revalidates every selected item before it commits.</summary>
    private static void AdmitItemInput(GameItem machine, MachineProfile profile,
        GameInventory slot)
    {
        var ids = MachineAdmission.InputIds(profile.Recipes.Select(entry => entry.Value));
        var allowed = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var id in ids) allowed.Add(id);
        var tags = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var tag in MachineAdmission.InputTags(profile.Recipes.Select(entry => entry.Value))) tags.Add(tag);
        // Replace the template/previous whitelist instead of AND-combining it
        // with the newly registered IDs, including other mods' extra recipes.
        slot.mayInventoryAddItemFunc = null;
        ContainerHelper.InitContainerItem(slot, machine,
            tags, allowed);
        Report(machine, slot, "item-input", $"{ids.Length} recipe ids");
    }

    /// <summary>Liquid inputs and the container output take water containers only.
    /// The template and recipe conditions stay batch-time rules, where the liquid
    /// runtime already evaluates them.</summary>
    private static void AdmitContainer(GameItem machine, GameInventory slot, string label)
    {
        slot.mayInventoryAddItemFunc = null;
        ContainerHelper.AllowOnlyTaggedItem(slot, LiquidContainerTag, false, false);
        Report(machine, slot, label, LiquidContainerTag);
    }

    /// <summary>Read the installed delegate back, so a missing whitelist or a
    /// managed bridge shows up here instead of only while the player drags.</summary>
    private static void Report(GameItem machine, GameInventory slot, string label, string detail)
    {
        try
        {
            var callback = slot.mayInventoryAddItemFunc;
            if (callback == null || callback.Pointer == IntPtr.Zero)
            {
                ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Admission] " +
                    $"{machine.identifier}: {label} has no native admission delegate");
                return;
            }
            var method = callback.Method;
            string target = callback.Target?.ToString() ?? "<null>";
            bool bridged = target.Contains("Il2CppToMonoDelegateReference", StringComparison.Ordinal) ||
                (method?.DeclaringType?.FullName?.StartsWith("System.Func", StringComparison.Ordinal) ?? false);
            ForgeMachineRegistrationApi.Log($"{(bridged ? "[WARN] " : "")}[NicokoboForge/Admission] " +
                $"{machine.identifier}: {label}; delegate={method?.DeclaringType?.FullName}.{method?.Name}; " +
                $"target={target}; interopBridge={bridged}; detail={detail}");
        }
        catch (Exception ex)
        {
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Admission] {machine.identifier}: " +
                $"{label} readback failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
