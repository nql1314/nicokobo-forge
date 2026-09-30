using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

internal static partial class ForgeMachineRuntime
{
    private sealed record Slots(GameSlotInventory Battery, GameInventory Module,
        GameGridInventory Input, GameInventory Output,
        GameSlotInventory? Water);
    private sealed class Mutation(GameItem item, int oldCount)
    {
        internal GameItem Item { get; } = item;
        internal int OldCount { get; } = oldCount;
        internal bool Detached { get; set; }
    }

    private static readonly Dictionary<IntPtr, Slots> Windows = new();
    private static readonly HashSet<(int Slot, string Run, int Day, int Item)> Committed = new();
    private static GameInventory? _main;
    private static GameInventory? _back;
    private static (int Slot, string Run, int Day) _night;

    internal static void BeforeLoadGame()
    {
        Windows.Clear();
        Committed.Clear();
        _main = null;
        _back = null;
        ResetLiquidRuntime();
    }

    internal static void AfterLoadGame(PlayerStore store)
    {
        try
        {
            foreach (var (machine, profile) in FindMachines(store))
                if (profile.Mode == ForgeMachineProcessMode.NightlyLiquid)
                    try { ConfigureLiquidMachine(machine); }
                    catch (Exception ex)
                    {
                        ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                            $"liquid rebind failed for {machine.identifier}: " +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"liquid rebind failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void AfterDecodeSaveItem(PlayerStore store, string json,
        GameInventory inventory)
    {
        if (store == null || inventory == null ||
            inventory.Pointer == IntPtr.Zero || string.IsNullOrEmpty(json)) return;
        if (string.Equals(json, store.mainInvJSON, StringComparison.Ordinal))
            _main = inventory;
        else if (string.Equals(json, store.backInvJSON, StringComparison.Ordinal))
            _back = inventory;
    }

    internal static void AfterCreateMachineInventoryWindow(
        Il2CppSystem.ValueTuple<PixelWindow, GameSlotInventory, GameInventory,
            GameGridInventory, GameInventory, GameSlotInventory> result)
    {
        if (result == null || result.Item1 == null ||
            result.Item1.Pointer == IntPtr.Zero) return;
        Windows[result.Item1.Pointer] = new(result.Item2, result.Item3,
            result.Item4, result.Item5, result.Item6);
    }

    private static bool TryResolveSlots(PixelWindow window, out Slots slots)
    {
        if (Windows.TryGetValue(window.Pointer, out slots!)) return true;
        var nodes = window.children;
        if (nodes == null || nodes.Count < 4)
        {
            slots = null!;
            return false;
        }
        GameSlotInventory? water = null;
        if (nodes.Count > 4)
            try { water = nodes[4].Cast<GameSlotInventory>(); }
            catch (InvalidCastException) { }
        slots = new(nodes[0].Cast<GameSlotInventory>(),
            nodes[1].Cast<GameInventory>(),
            nodes[2].Cast<GameGridInventory>(),
            nodes[3].Cast<GameInventory>(), water);
        Windows[window.Pointer] = slots;
        return true;
    }

    internal static void BeforeEndNight(PlayerStore store)
    {
        if (store == null || store.Pointer == IntPtr.Zero ||
            !ForgeMachineApi.HasMachines) return;
        try
        {
            int slot = store.saveSlotId;
            string run = store.runID;
            int day = StoreStation.GetDayCounter();
            if (slot < 0 || string.IsNullOrWhiteSpace(run) || day < 0) return;
            var night = (slot, run, day);
            if (_night != night)
            {
                Committed.Clear();
                _night = night;
            }
            var found = FindMachines(store);
            foreach (var (machine, profile) in found)
            {
                try
                {
                    int id = machine.GetUniqueID();
                    if (id <= 0) continue;
                    var key = (slot, run, day, id);
                    if (Committed.Contains(key)) continue;
                    string status = Process(machine, profile);
                    ForgeMachineApi.Log($"[INFO] [NicokoboForge/Machine] " +
                        $"machine={machine.identifier}; item={id}; status={status}");
                    Committed.Add(key);
                }
                catch (Exception ex)
                {
                    ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                        $"machine={machine.identifier}; failure={ex.GetType().Name}: {ex.Message}");
                }
            }
            ForgeMachineApi.Log($"[INFO] [NicokoboForge/Machine] " +
                $"night slot={slot}; day={day}; machines={found.Count}; " +
                $"main={_main != null}; back={_back != null}");
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"night scan failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static List<(GameItem Item, MachineProfile Profile)> FindMachines(PlayerStore store)
    {
        var found = new List<(GameItem Item, MachineProfile Profile)>();
        var visited = new HashSet<IntPtr>();
        int nodes = 0;
        // PlayerStore owns the authoritative recursive item walk. Prefer it
        // over reconstructing the save-bag graph: live runs and decoded saves
        // do not expose identical root inventory objects.
        var all = store.FindAllItem();
        if (all != null)
            for (int index = 0; index < all.Count; index++)
                VisitItem(all[index], found, visited, ref nodes);
        // New runs create live save bags without calling DecodeSaveItem. Read
        // those bags on every scan so machines placed before the first load
        // are included in the night that is about to be saved. These scans are
        // retained as bounded fallbacks for a future build where FindAllItem
        // might exclude an inventory family.
        var bags = store.saveBags;
        if (bags != null && bags.Pointer != IntPtr.Zero)
            foreach (var bag in bags.Values)
                if (bag != null && bag.Pointer != IntPtr.Zero &&
                    (bag.identifier == "save_bag" ||
                     bag.identifier == "back_inv_save_bag"))
                    VisitItem(bag, found, visited, ref nodes);
        VisitInventory(_main, found, visited, ref nodes);
        VisitInventory(_back, found, visited, ref nodes);
        VisitInventory(store.gridInv, found, visited, ref nodes);
        return found;
    }

    private static void VisitInventory(GameInventory? inventory,
        List<(GameItem Item, MachineProfile Profile)> found,
        HashSet<IntPtr> visited, ref int nodes)
    {
        if (inventory == null || inventory.Pointer == IntPtr.Zero ||
            !visited.Add(inventory.Pointer) || ++nodes > 512) return;
        var items = inventory.childItems;
        if (items == null) return;
        for (int index = 0; index < items.Count && index < 1024; index++)
            VisitItem(items[index], found, visited, ref nodes);
    }

    private static void VisitItem(GameItem? item,
        List<(GameItem Item, MachineProfile Profile)> found,
        HashSet<IntPtr> visited, ref int nodes)
    {
        if (item == null || item.Pointer == IntPtr.Zero ||
            !visited.Add(item.Pointer)) return;
        if (ForgeMachineApi.TryGet(item.identifier, out var profile) && profile != null)
            found.Add((item, profile));
        var children = item.children;
        if (children == null) return;
        for (int index = 0; index < children.Count && index < 64; index++)
            try
            {
                VisitInventory(children[index].Cast<GameInventory>(), found,
                    visited, ref nodes);
            }
            catch (InvalidCastException) { }
    }

    private static string Process(GameItem machine,
        MachineProfile profile)
    {
        if (profile.Mode == ForgeMachineProcessMode.NightlyLiquid)
            return ProcessLiquid(machine, profile);
        if (!TrySlots(machine, out var input, out var output,
                out var battery, out var module, out _))
            return "slots-unavailable";
        if (input!.childItems == null || input.childItems.Count == 0)
            return "no-input";
        if (profile.NativeMachine)
        {
            if (profile.NativeBatchProbe == null)
            {
                // Registrations without a probe keep the earlier rule: any item
                // outside the declared feedstock family keeps the day native.
                for (int index = 0; index < input.childItems.Count; index++)
                    if (!profile.IsFeedstock(input.childItems[index].identifier) &&
                        !ForgeMachineHooks.IsFurnaceAuxiliary(
                            input.childItems[index].identifier))
                        return "mixed-native-input";
            }
            else if (NativeBatchAvailable(profile, machine, input))
                return "native-batch-night";
        }
        var entry = profile.Recipes.FirstOrDefault(candidate =>
            TrySelect(input, candidate.Value, out _));
        if (entry == null) return "input-short-or-condition";
        var recipe = entry.Value;
        int count = recipe.ResolveOutputCount?.Invoke(machine) ?? recipe.OutputCount;
        int power = recipe.ResolvePowerCost?.Invoke(machine) ?? recipe.PowerCost;
        return Execute(machine, input, output!, battery!, module,
            recipe, count, power);
    }

    /// <summary>Asks the native machine's owner whether the game's own batch
    /// can run from this input. A probe failure hands the day to the native
    /// cycle, so registered recipes never take a night the game can use.</summary>
    internal static bool NativeBatchAvailable(MachineProfile profile,
        GameItem machine, GameInventory input)
    {
        if (profile.NativeBatchProbe == null) return false;
        try
        {
            return profile.NativeBatchProbe(machine, input);
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] native batch " +
                $"probe failed for {profile.MachineId}: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    private static bool TrySlots(GameItem machine, out GameInventory? input,
        out GameInventory? output, out GameItem? battery,
        out GameInventory? module, out GameSlotInventory? water)
    {
        input = null;
        output = null;
        battery = null;
        module = null;
        water = null;
        try
        {
            var children = machine.children;
            if (children == null || children.Count != 1) return false;
            var window = children[0].Cast<PixelWindow>();
            if (!TryResolveSlots(window, out var slots)) return false;
            battery = slots.Battery.childItem;
            module = slots.Module;
            input = slots.Input;
            output = slots.Output;
            water = slots.Water;
            return battery != null && battery.Pointer != IntPtr.Zero &&
                input.Pointer != IntPtr.Zero && output.Pointer != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"slot lookup failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool TrySelect(GameInventory input,
        ForgeMachineRecipe recipe, out List<(GameItem Item, int Count)> takes)
    {
        takes = new();
        var items = input.childItems;
        if (items == null) return false;
        var positions = new Dictionary<IntPtr, int>();
        foreach (var ingredient in recipe.Inputs)
        {
            int remaining = ingredient.Count;
            for (int index = 0; index < items.Count && remaining > 0; index++)
            {
                var item = items[index];
                if (item == null || item.Pointer == IntPtr.Zero ||
                    item.identifier != ingredient.ItemId || item.unitCount <= 0 ||
                    !GeneralHelper.IsItemOwned(item) ||
                    ingredient.Condition?.Invoke(item) == false) continue;
                int id = item.GetUniqueID();
                if (id <= 0) return false;
                int position = positions.GetValueOrDefault(item.Pointer, -1);
                int alreadyTaken = position < 0 ? 0 : takes[position].Count;
                int available = item.unitCount - alreadyTaken;
                int count = Math.Min(remaining, available);
                if (count <= 0) continue;
                if (position < 0)
                {
                    positions.Add(item.Pointer, takes.Count);
                    takes.Add((item, count));
                }
                else takes[position] = (item, alreadyTaken + count);
                remaining -= count;
            }
            if (remaining > 0)
            {
                takes.Clear();
                return false;
            }
        }
        return takes.Count > 0;
    }

    private static string Execute(GameItem machine, GameInventory input,
        GameInventory output, GameItem battery, GameInventory? module,
        ForgeMachineRecipe recipe, int count, int power)
    {
        if (count <= 0 || count > 256 || power < 0)
            return "invalid-batch";
        if (!TrySelect(input, recipe, out var takes))
            return "input-changed";
        if (!PowerHelper.CanDrawPowerSource(battery, power))
            return "power-unavailable";
        int energy = battery.GetTagReadonly("power_source_item_energy")
            ?.valueInt ?? -1;
        if (energy < power) return "battery-state-unavailable";
        var products = new List<GameItem>(count);
        var mutations = new List<Mutation>();
        bool committed = false;
        bool powerTouched = false;
        try
        {
            if (!CreateProducts(recipe.OutputItemId, count, products))
                return "output-factory-failed";
            int previousOutputCount = output.childItems?.Count ?? 0;
            if (recipe.PrepareOutput != null)
            {
                var selectedInputs = takes.SelectMany(take =>
                    Enumerable.Repeat(take.Item, take.Count)).ToArray();
                foreach (var product in products)
                    recipe.PrepareOutput(machine, input, selectedInputs, product);
            }
            foreach (var take in takes)
            {
                var item = take.Item;
                if (item.parentInventory?.Pointer != input.Pointer ||
                    item.unitCount < take.Count) return "input-changed";
                var mutation = new Mutation(item, item.unitCount);
                mutations.Add(mutation);
                if (take.Count == item.unitCount)
                {
                    bool old = input.overrideLockRemove;
                    input.overrideLockRemove = true;
                    try
                    {
                        if (!input.Expel(item) || item.parentInventory != null)
                            return "input-expel-failed";
                        mutation.Detached = true;
                    }
                    finally { input.overrideLockRemove = old; }
                }
                else
                {
                    item.SetUnitCount(item.unitCount - take.Count);
                    if (item.unitCount != mutation.OldCount - take.Count)
                        return "input-count-failed";
                }
            }
            foreach (var product in products)
            {
                if (!PlaceOutput(output, product))
                    return "output-placement-failed";
                if (product.parentInventory?.Pointer != output.Pointer)
                    return "output-readback-failed";
            }
            if (output.childItems?.Count != previousOutputCount + count)
                return "output-count-readback-failed";
            powerTouched = true;
            if (!PowerHelper.DrawPowerSource(battery, power))
                return "power-draw-failed";
            if (battery.GetTagReadonly("power_source_item_energy")
                ?.valueInt != energy - power)
                return "power-readback-failed";
            committed = true;
            foreach (var mutation in mutations)
                if (mutation.Detached)
                    try { mutation.Item.Destroy(); }
                    catch (Exception ex)
                    {
                        ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                            $"consumed item cleanup failed: {ex.GetType().Name}: {ex.Message}");
                    }
            NotifyWork(machine, module);
            return $"produced:{recipe.RecipeId}x{count}; power={power}";
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] batch failed: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return "exception";
        }
        finally
        {
            if (!committed)
            {
                if (powerTouched) RestoreBattery(battery, energy);
                bool recalled = true;
                foreach (var product in products)
                    try { product.Destroy(); }
                    catch { recalled = false; }
                if (!recalled)
                    ForgeMachineApi.Log("[WARN] [NicokoboForge/Machine] CRITICAL output recall failed");
                else
                    for (int index = mutations.Count - 1; index >= 0; index--)
                    {
                        var mutation = mutations[index];
                        try
                        {
                            if (mutation.Item.parentInventory == null &&
                                !input.UncheckedAccept(mutation.Item))
                                throw new InvalidOperationException("Input restore rejected");
                            if (mutation.Item.parentInventory?.Pointer != input.Pointer)
                                throw new InvalidOperationException("Input parent changed");
                            if (mutation.Item.unitCount != mutation.OldCount)
                                mutation.Item.SetUnitCount(mutation.OldCount);
                        }
                        catch (Exception ex)
                        {
                            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                                $"CRITICAL input restore failed: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
            }
        }
    }

}
