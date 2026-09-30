using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Read live machine slots and adapter readiness. Registration and
/// resource arithmetic use their own APIs.</summary>
public static class ForgeMachineRuntimeApi
{
    public static bool RuntimeInstalled => ForgeMachineHooks.Installed;
    public static bool NativeFurnaceExtensionInstalled => ForgeMachineHooks.NativeFurnaceInstalled;
    public static bool TryGetInventory(GameItem machine, out ForgeMachineInventory? inventory) => ForgeMachineUi.TryGet(machine, out inventory);
}
