using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class ConsumerChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        var device = new GameItemElement { identifier = "checks.powered_glasses" };
        var slot = new GameSlotInventory();
        var local = new GameItemElement { Energy = 3 };
        slot.UncheckedAccept(local);
        using var consumer = ForgeExternalPowerApi.RegisterConsumer("checks", device.identifier, _ => slot.childItem);
        Check(ForgeExternalPowerApi.CanSupplyDevice(device) && ForgeExternalPowerApi.GetDevicePowerSource(device) == local,
            "A declared powered item resolves its real source without being a registered recipe machine");
        bool duplicate = false;
        try { ForgeExternalPowerApi.RegisterConsumer("checks", device.identifier, _ => local); }
        catch (ArgumentException) { duplicate = true; }
        Check(duplicate, "A second source declaration cannot replace an existing powered item");
        Check(ForgePowerApi.GetDeviceEnergy(device) == 3 && local.Energy == 3,
            "Without a grid provider a powered item reads its actual battery without drawing");
        var debit = ForgePowerApi.TryDebitDevice(device, 2);
        Check(debit.Status == ForgeEnergyDebitStatus.Committed && local.Energy == 1 && local.parentInventory == slot,
            "Without logistics the same powered item still debits its actual local battery");
        Check(ForgePowerApi.TryDebitDevice(device, 2).Status == ForgeEnergyDebitStatus.Insufficient && local.Energy == 1,
            "Local insufficient power does not partially debit a powered item");

        local.Energy = 3;
        var supply = new DeviceSupply(device, slot) { Grid = 100 };
        using var provider = ForgeExternalPowerApi.RegisterMachine("checks", "checks.consumer_power", supply.Energy, supply.Plan);
        Check(ForgePowerApi.GetDeviceEnergy(device) == 20 && supply.Plans == 0 && local.Energy == 3 && supply.Grid == 100,
            "Device energy queries expose grid availability without pre-debiting");
        debit = ForgePowerApi.TryDebitDevice(device, 8);
        Check(debit.Status == ForgeEnergyDebitStatus.Committed && local.Energy == 0 && supply.Grid == 95,
            "An actual device operation uses three local energy and only five from its grid");
        Check(debit.Before == 20 && debit.After == 20 && supply.Commits == 1 && supply.Disposals == 1 && !supply.Busy,
            "A capacity-capped availability read is not mistaken for a failed debit and disposes the receipt once");
        Check(ForgePowerApi.TryDebitDevice(device, 21).Status == ForgeEnergyDebitStatus.Insufficient && supply.Plans == 1,
            "A device operation cannot exceed its physical source capacity");
        int plans = supply.Plans;
        Check(ForgePowerApi.TryDebitDevice(device, 0).Status == ForgeEnergyDebitStatus.Committed && supply.Plans == plans,
            "A zero-cost device operation does not create a power receipt");
        Check(ForgePowerApi.TryDebitDevice(device, -1).Status == ForgeEnergyDebitStatus.Invalid && supply.Plans == plans,
            "A negative device cost cannot reach a power provider");
        supply.RejectPlan = true;
        Check(ForgePowerApi.TryDebitDevice(device, 5).Status == ForgeEnergyDebitStatus.Restored && supply.Grid == 95 && !supply.Busy,
            "A provider rejection leaves local and grid energy intact");
        supply.RejectPlan = false; supply.FailVerify = true;
        Check(ForgePowerApi.TryDebitDevice(device, 5).Status == ForgeEnergyDebitStatus.Restored && local.Energy == 0 && supply.Grid == 95,
            "Failed debit readback restores both stores instead of granting the device action");
        Check(supply.Commits == 1 && !supply.Busy, "A restored operation releases the receipt without recording consumption");
        supply.FailRestore = true;
        Check(ForgePowerApi.TryDebitDevice(device, 5).Status == ForgeEnergyDebitStatus.Faulted && supply.Faults == 0 && !supply.Busy,
            "Failed power compensation reports its result and releases the lease without disabling the supplier");
        supply.FailRestore = supply.FailVerify = false;
        Check(ForgePowerApi.TryDebitDevice(device, 5).Status == ForgeEnergyDebitStatus.Committed && supply.Grid == 85 && supply.Faults == 0,
            "The next device operation uses actual remaining power after failed compensation");
        slot.Expel(local);
        plans = supply.Plans;
        Check(ForgePowerApi.TryDebitDevice(device, 5).Status == ForgeEnergyDebitStatus.Invalid && supply.Plans == plans,
            "Grid power never bypasses a missing real device battery");
        slot.UncheckedAccept(local);
        supply.Enabled = false; local.Energy = 9;
        Check(ForgePowerApi.TryDebitDevice(device, 4).Status == ForgeEnergyDebitStatus.Committed && local.Energy == 5,
            "A provider that does not claim this operation preserves normal local battery use");
        ForgeCapabilities.Current.KnownGameBuild = false;
        Check(ForgePowerApi.TryDebitDevice(device, 4).Status == ForgeEnergyDebitStatus.Invalid && local.Energy == 5,
            "An unknown game build cannot debit a powered item");
        ForgeCapabilities.Current.KnownGameBuild = true;
        consumer.Dispose();
        Check(!ForgeExternalPowerApi.CanSupplyDevice(device) && ForgeExternalPowerApi.GetDevicePowerSource(device) == null,
            "Uninstalling a content consumer releases its source declaration");
        return checks;
    }

    private sealed class DeviceSupply(GameItem device, GameSlotInventory slot)
    {
        internal int Grid, Plans, Commits, Faults, Disposals;
        internal bool Enabled = true, Busy, RejectPlan, FailVerify, FailRestore;
        internal int Energy(GameItem value) => !Enabled || value != device ? -1 : slot.childItem == null ? 0 : Math.Min(20, slot.childItem.Energy + Grid);
        internal IForgePowerTransaction? Plan(GameItem value, int cost)
        {
            Plans++;
            if (value != device || Busy || RejectPlan || slot.childItem is not { } local) return null;
            Busy = true; return new Receipt(this, slot, local, cost);
        }
        private sealed class Receipt(DeviceSupply owner, GameSlotInventory slot, GameItem local, int cost) : IForgePowerTransaction
        {
            private readonly int _local = local.Energy, _grid = owner.Grid;
            private readonly int _needed = Math.Max(0, cost - local.Energy);
            private bool _debited, _verified;
            public bool Validate() => !_debited && slot.childItem == local && local.Energy == _local && owner.Grid == _grid;
            public void Debit() { local.Energy = Math.Max(0, _local - cost); owner.Grid -= _needed; _debited = true; }
            public bool Verify() => _verified = _debited && !owner.FailVerify && slot.childItem == local &&
                local.Energy == Math.Max(0, _local - cost) && owner.Grid == _grid - _needed;
            public bool Restore()
            {
                _verified = false;
                if (owner.FailRestore) return false;
                local.Energy = _local; owner.Grid = _grid; _debited = false; return true;
            }
            public void Fault(string reason) => owner.Faults++;
            public void Dispose() { if (_verified) owner.Commits++; owner.Busy = false; owner.Disposals++; }
        }
    }
}
