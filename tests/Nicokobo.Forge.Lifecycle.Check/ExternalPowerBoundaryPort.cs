namespace Nicokobo.Forge;

// This lifecycle suite keeps its original ordinary-battery boundary. Real
// connector hooks and cross-battery native commits require the native suite.
internal static class ForgeExternalPowerApi
{
    internal static bool IsConnector(Il2Cpp.GameItem item) => false;
    internal static int GetEnergy(Il2Cpp.GameItem item) => 0;
    internal static IForgePowerTransaction? Plan(Il2Cpp.GameItem machine, Il2Cpp.GameItem connector, int cost) => null;
}
