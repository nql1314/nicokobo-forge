using Il2Cpp;

namespace Nicokobo.Forge;

// Shared native steps; item and liquid rollback remain in their transactions.
internal static partial class ForgeMachineRuntime
{
    private static bool CreateProducts(string itemId, int count, List<GameItem> products)
    {
        for (int index = 0; index < count; index++)
        {
            var product = DirectoryMaster.Item(itemId, true);
            if (product == null || product.Pointer == IntPtr.Zero) return false;
            products.Add(product); // Also recall a factory result with a wrong ID.
            if (product.identifier != itemId) return false;
        }
        return true;
    }

    private static void NotifyWork(GameItem machine, GameInventory? module)
    {
        try { MachineHelper.OnMachineActioned(machine, module); }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"work notification failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool PlaceOutput(GameInventory output, GameItem product)
    {
        var slot = output.TryFindOneValidInventorySlot(product);
        if (slot != null && slot.Pointer != IntPtr.Zero &&
            slot.targetItem != null)
        {
            // The native search offers a matching stack before it scans grid
            // cells. Probe with a distinct ID to skip that match, restoring
            // the real ID before accepting the slot.
            string identifier = product.identifier;
            try
            {
                product.identifier = identifier + ".forge_empty_slot_probe";
                slot = output.TryFindOneValidInventorySlot(product);
            }
            finally { product.identifier = identifier; }
        }
        if (slot != null && slot.Pointer != IntPtr.Zero &&
            slot.targetItem == null && slot.IsValid())
        {
            slot.AcceptUnchecked();
            return product.parentInventory?.Pointer == output.Pointer;
        }
        // The native grid has no free shape for this item. Preserve the
        // furnace's overflow behavior only after the placement search fails.
        return output.UncheckedAccept(product);
    }

    private static bool RestoreBattery(GameItem battery, int energy)
    {
        try
        {
            PowerHelper.SetPowerSourceAt(battery, energy);
            if (battery.GetTagReadonly("power_source_item_energy")
                ?.valueInt != energy)
                throw new InvalidOperationException("Battery readback mismatch");
            return true;
        }
        catch (Exception ex)
        {
            ForgeMachineApi.Log($"[WARN] [NicokoboForge/Machine] " +
                $"CRITICAL battery restore failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
