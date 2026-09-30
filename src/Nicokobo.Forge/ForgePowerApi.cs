using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Game power operations are isolated here; batch cost arithmetic is
/// available without game state through ForgeMachinePowerMath.</summary>
public static class ForgePowerApi
{
    public static int GetMachineCost(GameItem machine)
    {
        Require(machine);
        return MachineryHelper.GetMachinePowerUsage(machine);
    }
    public static int GetEnergy(GameItem source)
    {
        Require(source);
        return PowerHelper.GetAvailableEnergyFromItem(source, false);
    }
    public static ForgeEnergyDebitResult TryDebit(GameItem source, int cost)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || source == null || source.Pointer == IntPtr.Zero || cost < 0)
            return new(ForgeEnergyDebitStatus.Invalid, 0, 0);
        int units = source.unitCount;
        var parent = source.parentInventory?.Pointer ?? IntPtr.Zero;
        return EnergyDebit.Execute(cost,
            () => (source.parentInventory?.Pointer ?? IntPtr.Zero) == parent ? GetEnergy(source) : -1,
            desired => PowerHelper.TryRemoveEnergy(source, desired, false),
            before =>
            {
                if ((source.parentInventory?.Pointer ?? IntPtr.Zero) != parent) return false;
                if (source.identifier == "energy_credit") { source.SetUnitCount(units); return source.unitCount == units; }
                PowerHelper.SetPowerSourceAt(source, before);
                return true;
            });
    }
    private static void Require(GameItem item)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || item == null || item.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Power adapter or item unavailable");
    }
    internal static int? ReadSource(GameItem source) => source.GetTagReadonly("power_source_item_energy")?.valueInt;
    internal static bool CanDrawSource(GameItem source, int cost) => PowerHelper.CanDrawPowerSource(source, cost);
    internal static bool DrawSource(GameItem source, int cost) => PowerHelper.DrawPowerSource(source, cost);
    internal static void RestoreSource(GameItem source, int energy) => PowerHelper.SetPowerSourceAt(source, energy);
}
