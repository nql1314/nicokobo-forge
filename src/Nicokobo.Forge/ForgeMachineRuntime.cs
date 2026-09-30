using System.Collections.ObjectModel;
using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

internal static partial class ForgeMachineRuntime
{
    private sealed record ItemReceipt(GameItem Item, int Count, int Units, int Id, string Identifier, long Value);
    private sealed record LiquidChange(GameItem Container, GameInventory Slot,
        ForgeMachineLiquidSnapshot Before, ForgeMachineLiquidSnapshot After);
    private sealed record BatchPlan(ForgeMachineBatchContext Context, ForgeMachineRecipe Recipe,
        ForgeMachineInventory Inventory, IReadOnlyList<ItemReceipt> Takes,
        IReadOnlyList<LiquidChange> Liquids, IReadOnlyList<ForgeMachineLiquidPart>? Contents,
        LiquidChange? ContainerOutput, GameItem? Battery, int Energy, int Power);
    private const string FaultTag = "NICOKOBO_FORGE_MACHINE_FAULT";
    private static readonly HashSet<(int Slot, string Run, int Day, int Item)> Attempted = [];
    private static readonly HashSet<int> Quarantined = [];
    private static (int Slot, string Run, int Day) _night;
    internal static void BeforeLoadGame()
    { ForgeMachineUi.Reset(); Attempted.Clear(); Quarantined.Clear(); _night = default; }
    internal static void AfterLoadGame(ForgeLifecycleContext context)
    {
        try { foreach (var (machine, _) in FindMachines(context.Items)) ForgeMachineUi.TryGet(machine, out _); }
        catch (Exception ex) { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] slot rebind failed: {ex.Message}"); }
    }

    internal static void BeforeEndNight(ForgeLifecycleContext context)
    {
        var store = context.Store;
        if (!ForgeMachineRuntimeApi.RuntimeInstalled || store == null || store.Pointer == IntPtr.Zero ||
            !ForgeMachineRegistrationApi.HasMachines) return;
        try
        {
            var night = (saveSlotId: context.Run.SlotId, runID: context.Run.RunId, context.Run.Day);
            if (night.saveSlotId < 0 || string.IsNullOrWhiteSpace(night.runID) || night.Item3 < 0) return;
            if (_night != night)
            {
                Attempted.Clear();
                if (_night.Slot != night.saveSlotId || _night.Run != night.runID) Quarantined.Clear();
                _night = night;
            }
            foreach (var (machine, profile) in FindMachines(context.Items))
            {
                try
                {
                    int id = machine.GetUniqueID();
                    if (id <= 0 || !Attempted.Add((night.saveSlotId, night.runID, night.Item3, id))) continue;
                    string status = Process(machine, profile);
                    ForgeMachineRegistrationApi.Log($"[INFO] [NicokoboForge/Machine] machine={machine.identifier}; item={id}; status={status}");
                }
                catch (Exception ex)
                { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] machine={machine.identifier}; failure={ex.Message}"); }
            }
        }
        catch (Exception ex) { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] night scan failed: {ex.Message}"); }
    }

    private static List<(GameItem Item, MachineProfile Profile)> FindMachines(IReadOnlyList<GameItem> items)
    {
        var found = new List<(GameItem, MachineProfile)>();
        foreach (var item in items)
        {
            if (ForgeMachineRegistrationApi.TryGet(item.identifier, out var profile) && profile != null) found.Add((item, profile));
        }
        return found;
    }

    internal static bool NativeOwnsNight(MachineProfile profile, GameItem machine, GameInventory? input)
    {
        var items = input?.childItems;
        if (items == null || items.Count == 0) return true;
        if (profile.NativeBatchProbe != null)
        {
            try { if (profile.NativeBatchProbe(machine, input!)) return true; }
            catch (Exception ex)
            { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] native probe failed: {ex.Message}"); return true; }
            return !Enumerable.Range(0, items.Count).Any(index => profile.IsFeedstock(items[index].identifier));
        }
        bool feedstock = false;
        for (int index = 0; index < items.Count; index++)
        {
            string id = items[index].identifier;
            feedstock |= profile.IsFeedstock(id);
            if (!profile.IsFeedstock(id) && id is not ("flux_agent" or "advanced_flux_agent")) return true;
        }
        return !feedstock;
    }

    private static string Process(GameItem machine, MachineProfile profile)
    {
        if (!GeneralHelper.IsItemOwned(machine)) return "machine-not-owned";
        if (Quarantined.Contains(machine.GetUniqueID()) || !string.IsNullOrEmpty(machine.GetTagReadonly(FaultTag)?.valueString))
            return "quarantined";
        if (profile.NativeMachine && !ForgeMachineHooks.NativeFurnaceInstalled) return "native-hooks-unavailable";
        if (!ForgeMachineUi.TryGet(machine, out var inventory) || inventory == null) return "slots-unavailable";
        if (profile.NativeMachine && NativeOwnsNight(profile, machine, inventory.ItemInput)) return "native-batch-night";
        string last = "input-short-or-condition";
        foreach (var entry in profile.Recipes)
        {
            if (!TryPlan(machine, profile, inventory, entry.Value, out var plan, out last)) continue;
            return Execute(plan!);
        }
        return last;
    }

    private static bool SelectItems(GameInventory? input, ForgeMachineRecipe recipe, out List<ItemReceipt> takes)
    {
        takes = [];
        if (recipe.ItemInputs.Count == 0) return true;
        if (input?.childItems == null) return false;
        var selected = new Dictionary<IntPtr, (GameItem Item, int Count)>();
        foreach (var ingredient in recipe.ItemInputs)
        {
            int remaining = ingredient.Count;
            var candidates = Enumerable.Range(0, input.childItems.Count).Select(index => input.childItems[index])
                .Where(item => item != null && item.Pointer != IntPtr.Zero && item.identifier == ingredient.ItemId &&
                    item.parentInventory?.Pointer == input.Pointer && item.unitCount > 0 && item.GetUniqueID() > 0 &&
                    GeneralHelper.IsItemOwned(item) && (ingredient.Condition?.Invoke(item) ?? true));
            if (ingredient.WholeStack) candidates = candidates.OrderByDescending(item => item.unitValue)
                .ThenByDescending(item => item.unitCount);
            foreach (var item in candidates)
            {
                int used = selected.GetValueOrDefault(item.Pointer).Count;
                if (ingredient.WholeStack && used != 0) continue;
                int count = ingredient.WholeStack ? item.unitCount : Math.Min(remaining, item.unitCount - used);
                if (count <= 0) continue;
                selected[item.Pointer] = (item, used + count);
                remaining -= ingredient.WholeStack ? 1 : count;
                if (remaining == 0) break;
            }
            if (remaining > 0) return false;
        }
        takes = selected.Values.Select(take => new ItemReceipt(take.Item, take.Count,
            take.Item.unitCount, take.Item.GetUniqueID(), take.Item.identifier, take.Item.unitValue)).ToList();
        return true;
    }

    private static bool TryPlan(GameItem machine, MachineProfile profile, ForgeMachineInventory inventory,
        ForgeMachineRecipe recipe, out BatchPlan? plan, out string status)
    {
        plan = null; status = "input-short-or-condition";
        try
        {
            if (!SelectItems(inventory.ItemInput, recipe, out var takes)) return false;
            var containers = new Dictionary<string, GameItem>(StringComparer.Ordinal);
            var changes = new List<LiquidChange>();
            var captured = new Dictionary<string, ForgeMachineLiquidSnapshot>(StringComparer.Ordinal);
            foreach (var rule in recipe.LiquidInputs)
            {
                var slot = inventory.LiquidInputs[rule.SlotId];
                var container = slot.childItem;
                var before = container == null ? null : ForgeLiquidApi.Capture(container);
                if (before == null || container!.parentInventory?.Pointer != slot.Pointer ||
                    !GeneralHelper.IsItemOwned(container) || (rule.Condition?.Invoke(container) == false))
                { status = "liquid-source-or-condition:" + rule.SlotId; return false; }
                if (profile.Template.LiquidInputs.Single(slotTemplate => slotTemplate.SlotId == rule.SlotId)
                    .Condition?.Invoke(container) == false)
                { status = "liquid-template-condition:" + rule.SlotId; return false; }
                containers.Add(rule.SlotId, container); captured.Add(rule.SlotId, before);
            }
            if (containers.Values.Select(item => item.Pointer).Distinct().Count() != containers.Count)
            { status = "container-slot-alias"; return false; }
            var context = new ForgeMachineBatchContext(machine,
                Array.AsReadOnly(takes.Select(take => new ForgeMachineItemTake(take.Item, take.Count)).ToArray()),
                new ReadOnlyDictionary<string, GameItem>(containers),
                recipe.Output is ForgeMachineItemOutput initial ? initial.Count : 1);
            int count = recipe.Output is ForgeMachineItemOutput output
                ? output.ResolveCount?.Invoke(context) ?? output.Count : 1;
            if (count is < 1 or > 256) { status = "output-count-invalid"; return false; }
            context = context with { OutputCount = count };
            foreach (var rule in recipe.LiquidInputs)
            {
                int parts = MachineLiquidMath.ToParts(rule.ResolveMillilitres?.Invoke(context) ?? rule.Millilitres);
                changes.Add(new(containers[rule.SlotId], inventory.LiquidInputs[rule.SlotId], captured[rule.SlotId],
                    MachineBatchMath.Consume(captured[rule.SlotId], parts, rule.LiquidId)));
            }
            var declarations = recipe.Output switch
            {
                ForgeMachineItemOutput item => item.Contents,
                ForgeMachineContainerOutput container => container.Contents,
                _ => null
            };
            var contents = declarations?.Select(part => new ForgeMachineLiquidPart(part.LiquidId,
                MachineLiquidMath.ToParts(part.ResolveMillilitres?.Invoke(context) ?? part.Millilitres))).ToArray();
            LiquidChange? containerOutput = null;
            if (recipe.Output is ForgeMachineContainerOutput)
            {
                var destination = inventory.Output.Cast<GameSlotInventory>().childItem;
                var before = destination == null ? null : ForgeLiquidApi.Capture(destination);
                if (before == null || destination!.parentInventory?.Pointer != inventory.Output.Pointer ||
                    !GeneralHelper.IsItemOwned(destination) ||
                    profile.Template.Output.ContainerCondition?.Invoke(destination) == false ||
                    containers.Values.Any(item => item.Pointer == destination.Pointer))
                { status = "output-container-missing-or-invalid"; return false; }
                containerOutput = new(destination, inventory.Output, before, MachineBatchMath.Add(before, contents!));
            }
            int power = ForgeMachinePowerMath.CalculateCost(profile.Power.ResolveCost?.Invoke(context) ?? profile.Power.Cost,
                count, profile.Power.PerOutput);
            var battery = inventory.Battery?.childItem;
            int energy = battery == null ? -1 : ForgePowerApi.ReadSource(battery) ?? -1;
            if ((profile.Template.Battery?.Required == true || power > 0) &&
                (battery == null || battery.Pointer == IntPtr.Zero || !GeneralHelper.IsItemOwned(battery) ||
                 battery.parentInventory?.Pointer != inventory.Battery!.Pointer || energy < power ||
                 !ForgePowerApi.CanDrawSource(battery, power)))
            { status = "power-unavailable"; return false; }
            plan = new(context, recipe, inventory, takes, changes, contents, containerOutput, battery, energy, power);
            if (!Unchanged(plan)) { plan = null; status = "input-changed"; return false; }
            status = "ready"; return true;
        }
        catch (Exception ex) { status = "batch-rule-invalid:" + ex.Message; return false; }
    }

    private static bool Unchanged(BatchPlan plan)
    {
        foreach (var ingredient in plan.Recipe.ItemInputs)
            if (plan.Takes.Where(take => take.Identifier == ingredient.ItemId &&
                    (ingredient.Condition?.Invoke(take.Item) ?? true)).Sum(take => take.Count) < ingredient.Count)
                return false;
        foreach (var rule in plan.Recipe.LiquidInputs)
            if (rule.Condition?.Invoke(plan.Context.LiquidContainers[rule.SlotId]) == false) return false;
        foreach (var take in plan.Takes)
            if (take.Item.parentInventory?.Pointer != plan.Inventory.ItemInput?.Pointer ||
                take.Item.GetUniqueID() != take.Id || take.Item.identifier != take.Identifier ||
                take.Item.unitCount != take.Units || take.Item.unitValue != take.Value ||
                !GeneralHelper.IsItemOwned(take.Item)) return false;
        foreach (var change in plan.Liquids.Concat(plan.ContainerOutput == null ? [] : new[] { plan.ContainerOutput }))
            if (change.Container.parentInventory?.Pointer != change.Slot.Pointer ||
                !GeneralHelper.IsItemOwned(change.Container) ||
                !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(change.Container), change.Before)) return false;
        return plan.Battery == null || (plan.Battery.parentInventory?.Pointer == plan.Inventory.Battery?.Pointer &&
            ForgePowerApi.ReadSource(plan.Battery) == plan.Energy);
    }

    private static void Quarantine(GameItem machine, string failure)
    {
        Quarantined.Add(machine.GetUniqueID());
        try
        {
            var tags = machine.state.dict;
            if (!tags.ContainsKey(FaultTag)) tags.Add(FaultTag, new TagState(FaultTag, FaultTag));
            tags[FaultTag].Enable();
            tags[FaultTag].SetString(failure);
            if (tags[FaultTag].valueString != failure) throw new InvalidOperationException("Fault marker readback failed");
        }
        catch (Exception ex) { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] fault marker failed: {ex.Message}"); }
        ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] CRITICAL rollback failed; quarantined={machine.GetUniqueID()}; {failure}");
    }
}
