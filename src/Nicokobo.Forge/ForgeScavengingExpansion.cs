using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class ForgeScavengingExpansion
{
    private static Action<string>? _log;
    internal static bool Installed { get; private set; }

    internal static void Install(bool knownBuild, Action<string> log)
    {
        if (Installed || !knownBuild) return;
        _log = log;
        try
        {
            NativeHookSet.Require(typeof(EmporiumEntry), "get_afterhourInventory", [], typeof(GameGridInventory));
            NativeHookSet.Require(typeof(EmporiumEntry), "get_afterhourWindow", [], typeof(PixelWindow));
            NativeHookSet.Require(typeof(GameGridInventory), nameof(GameGridInventory.SetShape),
                [typeof(int), typeof(int)], typeof(GameGridInventory));
            NativeHookSet.Require(typeof(GameGridInventory), "get_inventoryShape", [], typeof(GridShape));
            NativeHookSet.Require(typeof(PixelWindow), nameof(PixelWindow.Validate), [], typeof(void));
            NativeHookSet.Require(typeof(PixelWindow), "get_childElement", [], typeof(PixelElement));
            NativeHookSet.Require(typeof(PixelWindow), nameof(PixelWindow.Detach), [], typeof(bool));
            NativeHookSet.Require(typeof(PixelWindow), nameof(PixelWindow.Attach), [typeof(PixelElement)], typeof(bool));
            NativeHookSet.Require(typeof(GridPixelElement), "get_gridWidth", [], typeof(int));
            NativeHookSet.Require(typeof(GridPixelElement), "get_gridHeight", [], typeof(int));
            NativeHookSet.Require(typeof(GridPixelElement), nameof(GridPixelElement.GetElement),
                [typeof(int), typeof(int)], typeof(PixelElement));
            NativeHookSet.Require(typeof(GridPixelElement), nameof(GridPixelElement.Detach),
                [typeof(PixelElement)], typeof(bool));
            NativeHookSet.Require(typeof(GridPixelElement), nameof(GridPixelElement.AttachPos),
                [typeof(PixelElement), typeof(int), typeof(int)], typeof(bool));
            NativeHookSet.Require(typeof(EmporiumEntry), "get_Instance", [], typeof(EmporiumEntry));
            Installed = NativeHookSet.Install("nicokobo.forge.scavenging_expansion",
            [
                new(typeof(EmporiumEntry), nameof(EmporiumEntry.SetupAfterhourInv), [], typeof(void),
                    typeof(ForgeScavengingExpansion), Postfix: nameof(AfterSetup)),
                new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void),
                    typeof(ForgeScavengingExpansion), Prefix: nameof(BeforeLoad))
            ], log);
            if (Installed)
                log($"[INFO] [NicokoboForge/Scavenging] ground grid expanded to {ForgeNumbers.Inventory.ScavengingGridSide}x{ForgeNumbers.Inventory.ScavengingGridSide}");
        }
        catch (Exception ex)
        { log($"[WARN] [NicokoboForge/Scavenging] expansion disabled: {ex.Message}"); }
    }

    private static void AfterSetup(EmporiumEntry __instance) => Expand(__instance);
    private static void BeforeLoad()
    {
        if (!Installed) return;
        try { Expand(EmporiumEntry.Instance); }
        catch (Exception ex)
        { _log?.Invoke($"[WARN] [NicokoboForge/Scavenging] load expansion failed: {ex.Message}"); }
    }

    private static void Expand(EmporiumEntry? scene)
    {
        if (!Installed || scene == null || scene.Pointer == IntPtr.Zero) return;
        try
        {
            var grid = scene.afterhourInventory;
            var shape = grid?.inventoryShape;
            if (grid == null || grid.Pointer == IntPtr.Zero || shape == null || shape.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native scavenging grid unavailable");
            int width = Math.Max(shape.width, ForgeNumbers.Inventory.ScavengingGridSide);
            int height = Math.Max(shape.height, ForgeNumbers.Inventory.ScavengingGridSide);
            // Resize in place, before saved contents are restored; retain items
            // and native scavenging/transfer callbacks, including other expansions.
            if (shape.width != width || shape.height != height) grid.SetShape(width, height);
            var window = scene.afterhourWindow;
            if (window == null || window.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native scavenging window unavailable");
            CompactLayout(window, grid);
            window.Validate();
            if (grid.inventoryShape.width != width || grid.inventoryShape.height != height)
                throw new InvalidOperationException("Scavenging grid size readback failed");
        }
        catch (Exception ex)
        { _log?.Invoke($"[WARN] [NicokoboForge/Scavenging] expansion failed: {ex.Message}"); }
    }

    private static void CompactLayout(PixelWindow window, GameGridInventory grid)
    {
        var original = window.childElement?.TryCast<GridPixelElement>()
            ?? throw new InvalidOperationException("Native scavenging layout unavailable");
        if (original.GetElement(0, 0)?.Pointer != grid.Pointer)
            throw new InvalidOperationException("Scavenging ground layout mismatch");
        // LoadGame can see an already expanded grid. Keep the same compact
        // hierarchy on repeated calls instead of nesting it again.
        if (original.gridWidth == 1 && original.gridHeight == 2 &&
            original.GetElement(0, 1)?.TryCast<GridPixelElement>() is { } strip &&
            strip.gridWidth == 4 && strip.gridHeight == 1) return;
        if (original.gridWidth != 4 || original.gridHeight != 1)
            throw new InvalidOperationException("Native scavenging row layout mismatch");

        var compact = new GridPixelElement(1, 2, true);
        if (!window.Detach()) throw new InvalidOperationException("Scavenging window detach failed");
        try
        {
            // The native 4x1 row centers small pockets beside the tall ground.
            // Move its ground to an upper row; keep the existing pocket/backpack
            // strip below it, including equipped bags and all native callbacks.
            if (!original.Detach(grid.Cast<PixelElement>()) ||
                !compact.AttachPos(grid.Cast<PixelElement>(), 0, 0) ||
                !compact.AttachPos(original.Cast<PixelElement>(), 0, 1) ||
                !window.Attach(compact.Cast<PixelElement>()))
                throw new InvalidOperationException("Compact scavenging layout attachment failed");
            if (window.childElement?.Pointer != compact.Pointer ||
                compact.GetElement(0, 0)?.Pointer != grid.Pointer ||
                compact.GetElement(0, 1)?.Pointer != original.Pointer)
                throw new InvalidOperationException("Compact scavenging layout readback failed");
        }
        catch
        {
            try
            {
                if (window.childElement?.Pointer == compact.Pointer) window.Detach();
                compact.Detach(grid.Cast<PixelElement>());
                compact.Detach(original.Cast<PixelElement>());
                if (original.GetElement(0, 0) == null) original.AttachPos(grid.Cast<PixelElement>(), 0, 0);
                if (window.childElement == null) window.Attach(original.Cast<PixelElement>());
                window.Validate();
            }
            catch (Exception ex)
            { _log?.Invoke($"[WARN] [NicokoboForge/Scavenging] layout restoration failed: {ex.Message}"); }
            throw;
        }
    }
}
