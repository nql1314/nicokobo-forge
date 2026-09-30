using System.Text.Json;
using Il2Cpp;
using Nicokobo.Forge.Registration;
using Il2CppInterop.Runtime;

namespace Nicokobo.Forge;

internal static partial class ForgeMachineRuntime
{
    private const string LiquidProgressTag = "NICOKOBO_FORGE_LIQUID_PROGRESS_V1";
    private const int MaxLifetimeOutput = 1_000_000;
    /// <summary>Format of the progress record this build writes. Any other
    /// value belongs to an older Forge and is dropped, never trusted.</summary>
    private const int LiquidProgressVersion = 2;
    private enum ProgressLoad { None, Valid, Stale, Damaged }
    private sealed record LiquidProgress(int Version, int BatchVersion,
        string RecipeId,
        string InputItemId, int InputInstanceId, int TargetCount,
        int ProducedCount, long InitialValue, int FrozenQualityPoints);
    private sealed record LiquidPart(string Identifier, string Tag, int Amount);
    private sealed record LiquidSnapshot(int TotalParts,
        IReadOnlyList<LiquidPart> Parts);
    private sealed record LiquidInputBinding(GameItem Machine,
        Il2CppSystem.Func<GameItem, GameInventory, bool>? Prior);
    private sealed class InventoryCallbacks
    {
        internal bool AdmitWater(GameItem item, GameInventory inventory) =>
            CanAdmitWater(item, inventory);

        internal bool RemoveWorkpiece(GameItem item, GameInventory inventory) =>
            CanRemoveWorkpiece(item, inventory);
    }

    private static readonly Dictionary<IntPtr, LiquidInputBinding> LiquidInputs = new();
    private static readonly HashSet<IntPtr> CommittingLiquidInputs = new();
    private static readonly HashSet<int> QuarantinedLiquidMachines = new();
    private static readonly InventoryCallbacks Callbacks = new();
    private static readonly Func<GameItem, GameInventory, bool>
        ManagedWaterAdmission = Callbacks.AdmitWater;
    private static readonly Func<GameItem, GameInventory, bool>
        ManagedWorkpieceRemoval = Callbacks.RemoveWorkpiece;
    private static Il2CppSystem.Func<GameItem, GameInventory, bool>?
        _waterAdmission;
    private static Il2CppSystem.Func<GameItem, GameInventory, bool>?
        _workpieceRemoval;

    private static void ResetLiquidRuntime()
    {
        LiquidInputs.Clear();
        CommittingLiquidInputs.Clear();
        QuarantinedLiquidMachines.Clear();
    }

    internal static void ConfigureLiquidMachine(GameItem machine)
    {
        if (machine == null || machine.Pointer == IntPtr.Zero ||
            machine.children == null || machine.children.Count != 1)
            throw new InvalidOperationException("Liquid machine window missing");
        var window = machine.children[0].Cast<PixelWindow>();
        if (!TryResolveSlots(window, out var slots) ||
            slots.Water == null || slots.Water.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Liquid machine water slot missing");
        _waterAdmission ??= DelegateSupport.ConvertDelegate<
            Il2CppSystem.Func<GameItem, GameInventory, bool>>(
            ManagedWaterAdmission) ??
            throw new InvalidOperationException("Water admission delegate missing");
        _workpieceRemoval ??= DelegateSupport.ConvertDelegate<
            Il2CppSystem.Func<GameItem, GameInventory, bool>>(
            ManagedWorkpieceRemoval) ??
            throw new InvalidOperationException("Workpiece removal delegate missing");
        // Validate the native-to-managed bridge before either callback is
        // published to a slot. A broken predicate otherwise aborts every drag
        // while the game searches candidate inventory slots.
        try
        {
            if (_waterAdmission.Invoke(machine, slots.Water) ||
                !_workpieceRemoval.Invoke(machine, slots.Input))
                throw new InvalidOperationException("Inventory callback self-check failed");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Liquid machine inventory callbacks are not callable", ex);
        }
        EnsureLiquidProgressTag(machine);
        var priorWater = slots.Water.mayInventoryAddItemFunc;
        var priorRemoval = slots.Input.mayInventoryRemoveItemFunc;
        var prior = priorRemoval;
        if (priorRemoval?.Pointer == _workpieceRemoval.Pointer &&
            LiquidInputs.TryGetValue(slots.Input.Pointer, out var existing))
            prior = existing.Prior;
        try
        {
            LiquidInputs[slots.Input.Pointer] = new(machine, prior);
            slots.Water.mayInventoryAddItemFunc = _waterAdmission;
            slots.Input.mayInventoryRemoveItemFunc = _workpieceRemoval;
        }
        catch
        {
            slots.Water.mayInventoryAddItemFunc = priorWater;
            slots.Input.mayInventoryRemoveItemFunc = priorRemoval;
            LiquidInputs.Remove(slots.Input.Pointer);
            throw;
        }
    }

    private static bool CanAdmitWater(GameItem item, GameInventory _)
    {
        try
        {
            return item != null && item.Pointer != IntPtr.Zero &&
                WaterHelper.CanUseWaterContainer(item) &&
                WaterHelper.GetCurrentCapacityML(item) > 0;
        }
        catch { return false; }
    }

    private static bool CanRemoveWorkpiece(GameItem item,
        GameInventory inventory)
    {
        if (item == null || item.Pointer == IntPtr.Zero) return false;
        if (inventory == null ||
            !LiquidInputs.TryGetValue(inventory.Pointer, out var binding))
            return true;
        try
        {
            var progress = ReadLiquidProgress(binding.Machine, out var state);
            // Unreadable data keeps the workpiece pinned; a dropped record no
            // longer owns an input, so the player may take it back.
            if (state == ProgressLoad.Damaged) return false;
            if (CommittingLiquidInputs.Contains(inventory.Pointer))
                return binding.Prior?.Invoke(item, inventory) ?? true;
            if (progress != null &&
                item.GetUniqueID() == progress.InputInstanceId)
                return false;
            return binding.Prior?.Invoke(item, inventory) ?? true;
        }
        catch { return false; }
    }

    private static TagState EnsureLiquidProgressTag(GameItem machine)
    {
        var tags = machine.state?.dict ??
            throw new InvalidOperationException("Machine state missing");
        if (!tags.ContainsKey(LiquidProgressTag))
        {
            var created = new TagState(LiquidProgressTag,
                LiquidProgressTag);
            tags.Add(LiquidProgressTag, created);
            created.Enable();
            created.SetString("");
        }
        var tag = tags[LiquidProgressTag];
        tag.Enable();
        return tag;
    }

    private static LiquidProgress? ReadLiquidProgress(GameItem machine,
        out ProgressLoad state)
    {
        state = ProgressLoad.None;
        var tags = machine.state?.dict;
        if (tags == null || !tags.ContainsKey(LiquidProgressTag)) return null;
        string json = tags[LiquidProgressTag].valueString;
        if (string.IsNullOrWhiteSpace(json)) return null;
        LiquidProgress? value;
        try { value = JsonSerializer.Deserialize<LiquidProgress>(json); }
        catch (JsonException) { state = ProgressLoad.Damaged; return null; }
        if (value == null) { state = ProgressLoad.Damaged; return null; }
        // An older record cannot price this night: the machine's rules may have
        // changed underneath the stored target, so it restarts from the input
        // still sitting in the slot instead of finishing a stale batch.
        if (value.Version != LiquidProgressVersion || value.BatchVersion < 1)
        {
            state = ProgressLoad.Stale;
            return null;
        }
        bool valid = !string.IsNullOrWhiteSpace(value.RecipeId) &&
            !string.IsNullOrWhiteSpace(value.InputItemId) &&
            value.InputInstanceId > 0 &&
            value.TargetCount is >= 1 and <= MaxLifetimeOutput &&
            value.ProducedCount >= 1 &&
            value.ProducedCount < value.TargetCount &&
            value.InitialValue >= 0;
        state = valid ? ProgressLoad.Valid : ProgressLoad.Damaged;
        return valid ? value : null;
    }

    /// <summary>Drops a record this build cannot price. The input stays in the
    /// slot, so the machine simply starts a fresh batch under current rules.</summary>
    private static bool ClearLiquidProgress(GameItem machine)
    {
        try
        {
            var tag = EnsureLiquidProgressTag(machine);
            tag.SetString("");
            if (tag.valueString != "")
                throw new InvalidOperationException("Progress clear readback mismatch");
            ForgeMachineApi.Log("[INFO] [NicokoboForge/Machine] stale liquid " +
                "progress dropped; the batch restarts under current rules");
            return true;
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] stale liquid " +
                $"progress could not be dropped: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static string ProcessLiquid(GameItem machine,
        MachineProfile profile)
    {
        int machineId = machine.GetUniqueID();
        if (QuarantinedLiquidMachines.Contains(machineId))
            return "liquid-quarantined";
        if (!TrySlots(machine, out var input, out var output,
                out var battery, out var module, out var waterSlot) ||
            waterSlot == null || waterSlot.Pointer == IntPtr.Zero)
            return "liquid-slots-unavailable";
        var water = waterSlot.childItem;
        if (water == null || water.Pointer == IntPtr.Zero ||
            water.parentInventory?.Pointer != waterSlot.Pointer ||
            !GeneralHelper.IsItemOwned(water))
            return "water-source-missing";
        var progress = ReadLiquidProgress(machine, out var state);
        if (state == ProgressLoad.Damaged) return "liquid-progress-invalid";
        if (state == ProgressLoad.Stale)
        {
            if (!ClearLiquidProgress(machine))
                return "liquid-progress-clear-failed";
            progress = null;
        }
        foreach (var entry in profile.Recipes)
        {
            var recipe = entry.Value;
            if (progress != null && recipe.RecipeId != progress.RecipeId)
                continue;
            if (progress != null && progress.BatchVersion != recipe.ProgressVersion)
            {
                // The recipe prices batches differently than the record does:
                // drop the record and start a clean batch from the same input.
                if (!ClearLiquidProgress(machine))
                    return "liquid-progress-clear-failed";
                progress = null;
            }
            GameItem meat;
            if (progress == null)
            {
                if (recipe.AcceptsStackedInput)
                {
                    meat = LargestValueInput(input!, recipe);
                    if (meat == null) continue;
                }
                else
                {
                    if (!TrySelect(input!, recipe, out var takes) ||
                        takes.Count != 1 || takes[0].Count != 1)
                        continue;
                    meat = takes[0].Item;
                    if (meat.unitCount != 1) return "input-stack-unsupported";
                }
            }
            else
            {
                var children = input!.childItems;
                meat = null!;
                if (children != null)
                    for (int index = 0; index < children.Count; index++)
                    {
                        var item = children[index];
                        if (item != null && item.Pointer != IntPtr.Zero &&
                            item.identifier == progress.InputItemId &&
                            item.GetUniqueID() == progress.InputInstanceId &&
                            (recipe.AcceptsStackedInput || item.unitCount == 1) &&
                            GeneralHelper.IsItemOwned(item))
                        {
                            meat = item;
                            break;
                        }
                    }
                if (meat == null) continue;
            }
            return ExecuteLiquid(machine, input!, output!, battery!, module,
                water, waterSlot, meat, recipe, progress);
        }
        return progress == null ? "input-short-or-condition" :
            "retained-input-missing";
    }

    /// <summary>Picks the matching input with the largest piece value, so a
    /// batch spends the biggest pieces first and leaves the small ones behind.
    /// </summary>
    private static GameItem? LargestValueInput(GameInventory input,
        ForgeMachineRecipe recipe)
    {
        var items = input.childItems;
        if (items == null) return null;
        var ingredient = recipe.Inputs[0];
        GameItem? selected = null;
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item == null || item.Pointer == IntPtr.Zero ||
                item.identifier != ingredient.ItemId || item.unitCount <= 0 ||
                !GeneralHelper.IsItemOwned(item) ||
                ingredient.Condition?.Invoke(item) == false ||
                item.GetUniqueID() <= 0) continue;
            if (selected != null &&
                (item.unitValue < selected.unitValue ||
                 (item.unitValue == selected.unitValue &&
                  item.unitCount <= selected.unitCount))) continue;
            selected = item;
        }
        return selected;
    }

    private static string ExecuteLiquid(GameItem machine, GameInventory input,
        GameInventory output, GameItem battery, GameInventory? module,
        GameItem water, GameSlotInventory waterSlot, GameItem meat,
        ForgeMachineRecipe recipe, LiquidProgress? progress)
    {
        var requirement = recipe.Water!;
        if (!CanAdmitWater(water, waterSlot)) return "water-container-invalid";
        int nativePurity = WaterHelper.GetWaterPurity(water);
        var currentCondition =
            WaterFeatureHelper.GetConditionFromPurity(nativePurity);
        string conditionId = currentCondition?.identifier ?? "";
        bool pure = conditionId == ItemConditionList.CreatePureWater().identifier;
        bool high = conditionId ==
            ItemConditionList.CreateHighQualityWater().identifier;
        bool basic = conditionId == ItemConditionList.CreateBaseWater().identifier;
        int quality = pure ? 3 : high ? 2 : basic ? 1 : 0;
        if (quality == 0 || quality < requirement.MinimumQuality)
            return "water-quality-unmet";
        var beforeWater = CaptureLiquid(water);
        if (beforeWater == null) return "water-composition-unavailable";
        int target = progress?.TargetCount ??
            recipe.ResolveLifetimeOutputCount!(machine, meat);
        // A target of zero means the input cannot pay for one batch; the stack
        // stays untouched for a later night.
        if (target == 0) return "input-below-batch";
        if (target is < 1 or > MaxLifetimeOutput)
            return "liquid-target-invalid";
        int produced = progress?.ProducedCount ?? 0;
        if (target - produced <= 0) return "liquid-progress-invalid";
        // A recipe may price its own water draw instead of the fixed rule: the
        // resolver reads the input this batch spends, so the volume matches the
        // product it makes. Any bad answer fails the night closed.
        int millilitresPerOutput;
        int partsPerOutput;
        try
        {
            millilitresPerOutput = recipe.ResolveWaterMillilitres?.Invoke(machine,
                meat) ?? requirement.MillilitresPerOutput;
            partsPerOutput = MachineLiquidMath.PartsPerOutput(millilitresPerOutput);
        }
        catch (ArgumentOutOfRangeException) { return "water-rule-invalid"; }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] water volume " +
                $"resolver failed for {recipe.RecipeId}: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return "water-rule-invalid";
        }
        int count = MachineLiquidMath.AvailableOutputs(target, produced,
            beforeWater.TotalParts, partsPerOutput);
        if (count <= 0) return "water-unavailable";
        count = Math.Min(count, recipe.MaxOutputsPerNight);
        if (count > int.MaxValue / partsPerOutput)
            return "water-volume-overflow";
        int power = recipe.ResolvePowerCost?.Invoke(machine) ?? recipe.PowerCost;
        if (power < 0 || !PowerHelper.CanDrawPowerSource(battery, power))
            return "power-unavailable";
        int energy = battery.GetTagReadonly("power_source_item_energy")
            ?.valueInt ?? -1;
        if (energy < power) return "battery-state-unavailable";
        int previousOutputCount = output.childItems?.Count ?? 0;
        var tag = EnsureLiquidProgressTag(machine);
        string oldProgress = tag.valueString;
        var next = new LiquidProgress(LiquidProgressVersion,
            recipe.ProgressVersion, recipe.RecipeId,
            meat.identifier, meat.GetUniqueID(), target,
            produced + count, progress?.InitialValue ??
                (recipe.AcceptsStackedInput
                    ? MachineLiquidMath.MergedStackValue(meat.unitValue,
                        meat.unitCount) : meat.unitValue),
            progress?.FrozenQualityPoints ??
                MachineryHelper.GetCurrentQualityBonus(machine));
        string nextProgress = next.ProducedCount == next.TargetCount ? "" :
            JsonSerializer.Serialize(next);
        var products = new List<GameItem>(count);
        bool powerTouched = false;
        bool waterTouched = false;
        bool meatDetached = false;
        bool meatRemovalAttempted = false;
        int meatUnitsBefore = 0;
        bool committed = false;
        try
        {
            if (!CreateProducts(recipe.OutputItemId, count, products))
                return "output-factory-failed";
            foreach (var product in products)
            {
                if (!PlaceOutput(output, product) ||
                    product.parentInventory?.Pointer != output.Pointer)
                    return "output-placement-failed";
            }
            // The owner finishes each product before anything is spent, exactly
            // like the item path. A failing hook rolls the night back: the
            // products are recalled and the water, power and input stay put.
            if (recipe.PrepareOutput != null)
            {
                var batchInputs = Array.AsReadOnly(new[] { meat });
                foreach (var product in products)
                    recipe.PrepareOutput(machine, input, batchInputs, product);
            }
            if (output.childItems?.Count != previousOutputCount + count)
                return "output-count-readback-failed";
            powerTouched = true;
            if (!PowerHelper.DrawPowerSource(battery, power) ||
                battery.GetTagReadonly("power_source_item_energy")
                    ?.valueInt != energy - power)
                return "power-draw-failed";
            waterTouched = true;
            int consumeParts = MachineLiquidMath.DebitParts(count,
                partsPerOutput);
            WaterHelper.Remove(water, consumeParts);
            var afterWater = CaptureLiquid(water);
            if (afterWater == null ||
                afterWater.TotalParts != beforeWater.TotalParts - consumeParts ||
                water.parentInventory?.Pointer != waterSlot.Pointer)
                return "water-readback-failed";
            if (next.ProducedCount == next.TargetCount)
            {
                int consumed = recipe.ResolveConsumedUnits?.Invoke(meat,
                    next.TargetCount) ?? meat.unitCount;
                if (consumed < 1 || consumed > meat.unitCount)
                    return "spent-units-invalid";
                if (consumed == meat.unitCount)
                {
                    bool old = input.overrideLockRemove;
                    input.overrideLockRemove = true;
                    meatRemovalAttempted = true;
                    CommittingLiquidInputs.Add(input.Pointer);
                    try
                    {
                        if (!input.Expel(meat) || meat.parentInventory != null)
                            return "meat-expel-failed";
                        meatDetached = true;
                    }
                    finally
                    {
                        CommittingLiquidInputs.Remove(input.Pointer);
                        input.overrideLockRemove = old;
                    }
                }
                else
                {
                    // Loose pieces left over stay in the slot for a later batch.
                    meatUnitsBefore = meat.unitCount;
                    meat.SetUnitCount(meatUnitsBefore - consumed);
                    if (meat.unitCount != meatUnitsBefore - consumed)
                        return "meat-count-failed";
                }
            }
            tag.SetString(nextProgress);
            if (tag.valueString != nextProgress)
                return "progress-readback-failed";
            committed = true;
            if (meatDetached)
                try { meat.Destroy(); }
                catch (Exception ex)
                {
                    ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                        $"meat cleanup failed: {ex.GetType().Name}: {ex.Message}");
                }
            NotifyWork(machine, module);
            return $"produced:{recipe.RecipeId}x{count}; power={power}; " +
                $"waterMl={count * millilitresPerOutput}; " +
                $"progress={next.ProducedCount}/{next.TargetCount}";
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"liquid batch failed: {ex.GetType().Name}: {ex.Message}");
            return "liquid-exception";
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    if (tag.valueString != oldProgress)
                        tag.SetString(oldProgress);
                }
                catch (Exception ex)
                {
                    QuarantineLiquidMachine(machine, tag);
                    ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                        $"CRITICAL progress restore failed: {ex.GetType().Name}: {ex.Message}");
                }
                if (meatUnitsBefore > 0 && meat.unitCount != meatUnitsBefore &&
                    meat.parentInventory?.Pointer == input.Pointer)
                    try
                    {
                        meat.SetUnitCount(meatUnitsBefore);
                        if (meat.unitCount != meatUnitsBefore)
                            throw new InvalidOperationException(
                                "Meat count restore rejected");
                    }
                    catch (Exception ex)
                    {
                        QuarantineLiquidMachine(machine, tag);
                        ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                            $"CRITICAL meat count restore failed: " +
                            $"{ex.GetType().Name}: {ex.Message}");
                    }
                if (meatRemovalAttempted &&
                    meat.parentInventory?.Pointer != input.Pointer)
                    try
                    {
                        if (meat.parentInventory != null)
                            throw new InvalidOperationException(
                                "Meat moved to another inventory");
                        if (!input.UncheckedAccept(meat) ||
                            meat.parentInventory?.Pointer != input.Pointer)
                            throw new InvalidOperationException("Meat restore rejected");
                    }
                    catch (Exception ex)
                    {
                        QuarantineLiquidMachine(machine, tag);
                        ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                            $"CRITICAL meat restore failed: {ex.GetType().Name}: {ex.Message}");
                    }
                if (waterTouched && !RestoreLiquid(water, beforeWater))
                {
                    QuarantineLiquidMachine(machine, tag);
                    ForgeMachineApi.Log("[WARN] [NicokoboForge/Machine] " +
                        "CRITICAL liquid restore failed; machine quarantined");
                }
                if (powerTouched && !RestoreBattery(battery, energy))
                    QuarantineLiquidMachine(machine, tag);
                foreach (var product in products)
                    try { product.Destroy(); }
                    catch (Exception ex)
                    {
                        QuarantineLiquidMachine(machine, tag);
                        ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                            $"CRITICAL output recall failed: {ex.GetType().Name}: {ex.Message}");
                    }
            }
        }
    }

    private static void QuarantineLiquidMachine(GameItem machine, TagState tag)
    {
        QuarantinedLiquidMachines.Add(machine.GetUniqueID());
        try { tag.SetString("QUARANTINED"); }
        catch { /* The in-memory quarantine still blocks this session. */ }
    }

    private static LiquidSnapshot? CaptureLiquid(GameItem container)
    {
        var liquids = Liquid.Liquids;
        if (liquids == null) return null;
        var parts = new List<LiquidPart>();
        long total = 0;
        for (int index = 0; index < liquids.Count; index++)
        {
            var liquid = liquids[index];
            if (liquid == null || string.IsNullOrWhiteSpace(liquid.identifier) ||
                string.IsNullOrWhiteSpace(liquid.currentPart)) continue;
            int amount = container.GetTagReadonly(liquid.currentPart)
                ?.valueInt ?? 0;
            if (amount < 0) return null;
            total += amount;
            if (total > int.MaxValue) return null;
            parts.Add(new(liquid.identifier, liquid.currentPart, amount));
        }
        int nativeTotal = WaterHelper.GetTotalVolume(container);
        return nativeTotal >= 0 && nativeTotal == total ?
            new(nativeTotal, parts) : null;
    }

    private static bool RestoreLiquid(GameItem container,
        LiquidSnapshot before)
    {
        try
        {
            foreach (var part in before.Parts)
            {
                int current = container.GetTagReadonly(part.Tag)?.valueInt ?? 0;
                if (current > part.Amount) return false;
                if (current < part.Amount)
                    WaterHelper.AddLiquid(container, part.Identifier,
                        part.Amount - current);
            }
            var restored = CaptureLiquid(container);
            return restored != null &&
                restored.TotalParts == before.TotalParts &&
                restored.Parts.Count == before.Parts.Count &&
                restored.Parts.Zip(before.Parts).All(pair =>
                    pair.First.Identifier == pair.Second.Identifier &&
                    pair.First.Tag == pair.Second.Tag &&
                    pair.First.Amount == pair.Second.Amount);
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"liquid rollback exception: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
