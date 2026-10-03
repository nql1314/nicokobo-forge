using Il2Cpp;

namespace Nicokobo.Forge.Runtime;

// Same window and (1,1) slot as MachineHelper.GetModuleInv, with readiness
// checks for ordinary items and machines whose content window is not built yet.
internal static class NativeModuleInventory
{
    internal static GameGridInventory? Read(GameItem machine)
    {
        var window = machine.contentWindow;
        if (window == null || window.Pointer == IntPtr.Zero) return null;
        var child = window.childElement;
        if (child == null || child.Pointer == IntPtr.Zero) return null;
        var layout = child.TryCast<GridPixelElement>();
        if (layout == null || layout.Pointer == IntPtr.Zero ||
            layout.gridWidth <= 1 || layout.gridHeight <= 1) return null;
        var slot = layout.GetElement(1, 1);
        if (slot == null || slot.Pointer == IntPtr.Zero) return null;
        return slot.TryCast<GameGridInventory>();
    }
}
