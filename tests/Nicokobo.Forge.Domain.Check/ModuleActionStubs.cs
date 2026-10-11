// Offline ports for the production action registration path. No native class
// initialization, detours or game startup runs in these fixtures.
namespace Il2Cpp
{
    public sealed partial class GameGridInventory
    {
        public Il2CppSystem.Collections.Generic.List<GameItem> items = new();
    }
    public static class MachineHelper
    {
        public static void SetupModuleBay(GameInventory inventory, GameItem machine,
            Il2CppSystem.Action action, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray types) { }
    }
    public static class ModuleEffectHelper
    {
        public static void ActivateOnUseAction(GameItem item, GameInventory inventory, GameItem machine) { }
        public static void ComputeAllModuleTempStat(Il2CppSystem.Collections.Generic.List<GameItem> items) { }
    }
    public static class ModuleHelper
    {
        public static void ComputeModuleEffect(GameGridInventory inventory, object? unused, GameItem machine) { }
        public static void ApplyBasicModuleEffect(GameGridInventory inventory, object? unused, GameItem machine) { }
    }
}
namespace Il2CppSystem
{
    public sealed class Action(System.Action? callback = null)
    {
        public void Invoke() => callback?.Invoke();
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppStringArray(string[] values) : List<string>(values);
}
