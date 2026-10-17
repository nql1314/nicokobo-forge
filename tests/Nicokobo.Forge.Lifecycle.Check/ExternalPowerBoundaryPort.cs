namespace Nicokobo.Forge;

// The production planner runs against these power boundaries. Native hooks
// and cross-battery commits are outside this managed lifecycle suite.
internal static class ForgeExternalPowerApi
{
    internal static Func<Il2Cpp.GameItem, int>? MachineEnergy;
    internal static Func<Il2Cpp.GameItem, int, IForgePowerTransaction?>? MachinePlan;
    internal static bool TryGetMachineEnergy(Il2Cpp.GameItem machine, out int energy)
    { energy = MachineEnergy?.Invoke(machine) ?? -1; return energy >= 0; }
    internal static IForgePowerTransaction? PlanMachine(Il2Cpp.GameItem machine, int cost) => MachinePlan?.Invoke(machine, cost);
    internal static bool IsConnector(Il2Cpp.GameItem item) => false;
    internal static int GetEnergy(Il2Cpp.GameItem item) => 0;
    internal static IForgePowerTransaction? Plan(Il2Cpp.GameItem machine, Il2Cpp.GameItem connector, int cost) => null;
}
