using Il2Cpp;

namespace Nicokobo.Forge;

internal static partial class ForgeMachineRuntime
{
    private static string Execute(BatchPlan plan)
    {
        var products = new List<GameItem>();
        var steps = new List<MachineTransactionStep>();
        var detached = new List<GameItem>();
        int outputBefore = plan.Inventory.Output.childItems?.Count ?? 0;
        steps.Add(new("baseline", () =>
        {
            if (!Unchanged(plan)) throw new InvalidOperationException("batch state changed");
        }, () => RestoreBaseline(plan)));
        if (plan.Recipe.Output is ForgeMachineItemOutput itemOutput)
        {
            steps.Add(new("item-output", () =>
            {
                string outputId = itemOutput.ResolveItemId == null
                    ? itemOutput.ItemId : itemOutput.ResolveItemId(plan.Context);
                if (string.IsNullOrWhiteSpace(outputId) || outputId != outputId.Trim())
                    throw new InvalidOperationException("output item ID invalid");
                for (int index = 0; index < plan.Context.OutputCount; index++)
                {
                    var product = DirectoryMaster.Item(outputId, true);
                    if (product == null || product.Pointer == IntPtr.Zero) throw new InvalidOperationException("factory failed");
                    if (product.parentInventory != null || products.Any(other => other.Pointer == product.Pointer))
                        throw new InvalidOperationException("factory returned an existing item");
                    products.Add(product);
                    if (product.identifier != outputId || product.unitCount != 1 || product.GetUniqueID() <= 0)
                        throw new InvalidOperationException("factory contract failed");
                    itemOutput.PrepareItem?.Invoke(plan.Context, product);
                    if (product.identifier != outputId || product.unitCount != 1 || product.parentInventory != null)
                        throw new InvalidOperationException("prepare changed product identity");
                    if (plan.Contents != null)
                    {
                        var before = ForgeLiquidApi.Capture(product)
                            ?? throw new InvalidOperationException("product is not a liquid container");
                        if (!ForgeLiquidApi.Write(product, MachineBatchMath.Add(before, plan.Contents)))
                            throw new InvalidOperationException("product fill readback failed");
                    }
                }
                if (!Unchanged(plan)) throw new InvalidOperationException("inputs changed during output preparation");
                foreach (var product in products)
                    if (!PlaceOutput(plan.Inventory.Output, product)) throw new InvalidOperationException("warehouse full or placement failed");
                if (plan.Inventory.Output.childItems?.Count != outputBefore + products.Count)
                    throw new InvalidOperationException("warehouse count readback failed");
                if (!Unchanged(plan)) throw new InvalidOperationException("inputs changed during output placement");
            }, () => RecallProducts(plan.Inventory.Output, products, outputBefore)));
        }
        if (plan.ContainerOutput is { } output)
            steps.Add(new("container-output", () =>
            {
                if (!Unchanged(plan) || !ForgeLiquidApi.Write(output.Container, output.After) ||
                    output.Container.parentInventory?.Pointer != output.Slot.Pointer)
                    throw new InvalidOperationException("container fill readback failed");
            }, () => RestoreContainer(output)));
        foreach (var change in plan.Liquids)
            steps.Add(new("liquid-input", () =>
            {
                if (change.Container.parentInventory?.Pointer != change.Slot.Pointer ||
                    !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(change.Container), change.Before) ||
                    !ForgeLiquidApi.Write(change.Container, change.After) ||
                    change.Container.parentInventory?.Pointer != change.Slot.Pointer)
                    throw new InvalidOperationException("liquid debit readback failed");
            }, () => RestoreContainer(change)));
        if (plan.Power > 0)
            steps.Add(new("battery", () =>
            {
                if (plan.ExternalPower != null)
                {
                    if (!plan.ExternalPower.Validate()) throw new InvalidOperationException("external power changed");
                    plan.ExternalPower.Debit();
                    if (!plan.ExternalPower.Verify()) throw new InvalidOperationException("external power readback failed");
                    return;
                }
                if (plan.Battery == null || plan.Battery.parentInventory?.Pointer != plan.Inventory.Battery?.Pointer ||
                    ForgePowerApi.ReadSource(plan.Battery) != plan.Energy ||
                    !ForgePowerApi.DrawSource(plan.Battery, plan.Power) ||
                    ForgePowerApi.ReadSource(plan.Battery) != plan.Energy - plan.Power)
                    throw new InvalidOperationException("power debit readback failed");
            }, () =>
            {
                if (plan.ExternalPower != null) return plan.ExternalPower.Restore();
                ForgePowerApi.RestoreSource(plan.Battery!, plan.Energy);
                return plan.Battery!.parentInventory?.Pointer == plan.Inventory.Battery?.Pointer &&
                    ForgePowerApi.ReadSource(plan.Battery) == plan.Energy;
            }));
        foreach (var take in plan.Takes)
            steps.Add(new("item-input", () =>
            {
                if (take.Item.parentInventory?.Pointer != plan.Inventory.ItemInput!.Pointer ||
                    take.Item.unitCount != take.Units || take.Item.identifier != take.Identifier ||
                    take.Item.GetUniqueID() != take.Id) throw new InvalidOperationException("selected item changed");
                if (take.Count == take.Units)
                {
                    if (!Expel(plan.Inventory.ItemInput, take.Item) || take.Item.parentInventory != null)
                        throw new InvalidOperationException("input expel failed");
                    detached.Add(take.Item);
                }
                else
                {
                    take.Item.SetUnitCount(take.Units - take.Count);
                    if (take.Item.unitCount != take.Units - take.Count) throw new InvalidOperationException("input count readback failed");
                }
            }, () =>
            {
                if (take.Item.parentInventory == null && !plan.Inventory.ItemInput!.UncheckedAccept(take.Item)) return false;
                take.Item.SetUnitCount(take.Units);
                return take.Item.parentInventory?.Pointer == plan.Inventory.ItemInput!.Pointer &&
                    take.Item.unitCount == take.Units && take.Item.identifier == take.Identifier && take.Item.GetUniqueID() == take.Id;
            }));
        steps.Add(new("commit-readback", () =>
        {
            foreach (var take in plan.Takes)
                if (take.Item.GetUniqueID() != take.Id || take.Item.identifier != take.Identifier ||
                    (take.Count == take.Units ? take.Item.parentInventory != null :
                        take.Item.parentInventory?.Pointer != plan.Inventory.ItemInput!.Pointer ||
                        take.Item.unitCount != take.Units - take.Count))
                    throw new InvalidOperationException("input commit mismatch");
            foreach (var change in plan.Liquids.Concat(plan.ContainerOutput == null ? [] : new[] { plan.ContainerOutput }))
                if (change.Container.parentInventory?.Pointer != change.Slot.Pointer ||
                    !ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(change.Container), change.After))
                    throw new InvalidOperationException("liquid commit mismatch");
            if (plan.Battery != null && (plan.Battery.parentInventory?.Pointer != plan.Inventory.Battery?.Pointer ||
                (plan.ExternalPower != null ? !plan.ExternalPower.Verify() :
                    !ForgeExternalPowerApi.IsConnector(plan.Battery) && ForgePowerApi.ReadSource(plan.Battery) != plan.Energy - plan.Power)))
                throw new InvalidOperationException("battery commit mismatch");
            if (products.Any(product => product.parentInventory?.Pointer != plan.Inventory.Output.Pointer) ||
                (products.Count > 0 && plan.Inventory.Output.childItems?.Count != outputBefore + products.Count))
                throw new InvalidOperationException("output commit mismatch");
        }, () => true));
        if (plan.Transaction != null)
            steps.Add(new("recipe-transaction", () =>
            {
                if (!plan.Transaction.Validate()) throw new InvalidOperationException("recipe transaction changed");
                plan.Transaction.Apply(products.AsReadOnly());
                if (!plan.Transaction.Verify()) throw new InvalidOperationException("recipe transaction readback failed");
            }, plan.Transaction.Restore));
        var result = MachineTransaction.Run(steps, failure =>
            ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] rollback step failed: {failure}"));
        if (!result.Committed)
        {
            if (!result.Restored)
            {
                plan.ExternalPower?.Fault(result.Status); plan.Transaction?.Fault(result.Status);
                Quarantine(plan.Context.Machine, result.Status);
            }
            return result.Status;
        }
        // Destruction happens only after the resource transaction commits.
        foreach (var item in detached)
            try { item.Destroy(); }
            catch (Exception ex) { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] consumed item cleanup failed: {ex.Message}"); }
        try { if (plan.Inventory.Modules != null) MachineHelper.OnMachineActioned(plan.Context.Machine, plan.Inventory.Modules); }
        catch (Exception ex) { ForgeMachineRegistrationApi.Log($"[WARN] [NicokoboForge/Machine] module action failed: {ex.Message}"); }
        return $"produced:{plan.Recipe.RecipeId}x{plan.Context.OutputCount}; power={plan.Power}";
    }

    private static bool RestoreContainer(LiquidChange change) =>
        change.Container.parentInventory?.Pointer == change.Slot.Pointer &&
        (ForgeLiquidApi.Matches(ForgeLiquidApi.Capture(change.Container), change.Before) ||
         ForgeLiquidApi.Write(change.Container, change.Before));

    private static bool RestoreBaseline(BatchPlan plan)
    {
        bool restored = true;
        foreach (var take in plan.Takes)
            try
            {
                if (take.Item.parentInventory == null && !plan.Inventory.ItemInput!.UncheckedAccept(take.Item))
                { restored = false; continue; }
                if (take.Item.parentInventory?.Pointer != plan.Inventory.ItemInput!.Pointer)
                { restored = false; continue; }
                if (take.Item.unitCount != take.Units) take.Item.SetUnitCount(take.Units);
                if (take.Item.identifier != take.Identifier || take.Item.unitValue != take.Value ||
                    take.Item.GetUniqueID() != take.Id || take.Item.unitCount != take.Units) restored = false;
            }
            catch { restored = false; }
        foreach (var change in plan.Liquids.Concat(plan.ContainerOutput == null ? [] : new[] { plan.ContainerOutput }))
            try { if (!RestoreContainer(change)) restored = false; }
            catch { restored = false; }
        if (plan.ExternalPower != null)
            try { if (!plan.ExternalPower.Restore()) restored = false; } catch { restored = false; }
        else if (plan.Battery != null && !ForgeExternalPowerApi.IsConnector(plan.Battery))
            try
            {
                if (ForgePowerApi.ReadSource(plan.Battery) != plan.Energy)
                    ForgePowerApi.RestoreSource(plan.Battery, plan.Energy);
                if (plan.Battery.parentInventory?.Pointer != plan.Inventory.Battery?.Pointer ||
                    ForgePowerApi.ReadSource(plan.Battery) != plan.Energy) restored = false;
            }
            catch { restored = false; }
        return restored;
    }

    private static bool Expel(GameInventory inventory, GameItem item)
    {
        bool prior = inventory.overrideLockRemove; inventory.overrideLockRemove = true;
        try { return inventory.Expel(item); }
        finally { inventory.overrideLockRemove = prior; }
    }
    private static bool RecallProducts(GameInventory output, IReadOnlyList<GameItem> products, int beforeCount)
    {
        bool recalled = true;
        foreach (var product in products)
            try
            {
                if (product.parentInventory != null && (product.parentInventory.Pointer != output.Pointer || !Expel(output, product)))
                { recalled = false; continue; }
                if (product.parentInventory != null) { recalled = false; continue; }
                product.Destroy();
            }
            catch { recalled = false; }
        return recalled && (output.childItems?.Count ?? 0) == beforeCount;
    }
    private static bool PlaceOutput(GameInventory output, GameItem product)
    {
        // Native furnace outputs reject player insertion. Production bypasses
        // only that admission while retaining native shape/capacity checks.
        var admission = output.mayInventoryAddItemFunc;
        bool locked = output.overrideLockInsert;
        output.mayInventoryAddItemFunc = null;
        output.overrideLockInsert = false;
        try
        {
            var slot = output.TryFindOneValidInventorySlot(product);
            if (slot?.targetItem != null)
            {
                string id = product.identifier;
                try { product.identifier = id + ".forge_empty_slot_probe"; slot = output.TryFindOneValidInventorySlot(product); }
                finally { product.identifier = id; }
            }
            if (slot == null || slot.Pointer == IntPtr.Zero || slot.targetItem != null || !slot.IsValid()) return false;
            slot.AcceptUnchecked();
            return product.parentInventory?.Pointer == output.Pointer;
        }
        finally { output.mayInventoryAddItemFunc = admission; output.overrideLockInsert = locked; }
    }
}
