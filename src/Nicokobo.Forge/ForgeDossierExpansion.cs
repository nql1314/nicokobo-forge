using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class ForgeDossierExpansion
{
    private static Action<string>? _log;
    internal static bool Installed { get; private set; }

    internal static void Install(bool knownBuild, Action<string> log)
    {
        if (Installed || !knownBuild) return;
        _log = log;
        try
        {
            NativeHookSet.Require(typeof(GameItem), "get_contentWindow", [], typeof(PixelWindow));
            NativeHookSet.Require(typeof(PixelWindow), "get_childElement", [], typeof(PixelElement));
            NativeHookSet.Require(typeof(GameGridInventory), nameof(GameGridInventory.SetShape),
                [typeof(int), typeof(int)], typeof(GameGridInventory));
            NativeHookSet.Require(typeof(GameGridInventory), "get_inventoryShape", [], typeof(GridShape));
            NativeHookSet.Require(typeof(PixelWindow), nameof(PixelWindow.Validate), [], typeof(void));
            NativeHookSet.Require(typeof(EmporiumEntry), "get_Instance", [], typeof(EmporiumEntry));
            NativeHookSet.Require(typeof(EmporiumEntry), "get_dossier", [], typeof(GameItem));
            Installed = NativeHookSet.Install("nicokobo.forge.dossier_expansion",
            [
                new(typeof(MiscItemDirectory), nameof(MiscItemDirectory.Dossier), [], typeof(GameItem),
                    typeof(ForgeDossierExpansion), Postfix: nameof(AfterCreate)),
                new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void),
                    typeof(ForgeDossierExpansion), Prefix: nameof(BeforeLoad))
            ], log);
            if (Installed)
                log($"[INFO] [NicokoboForge/Dossier] {ForgeNumbers.Inventory.DossierGridSide}x" +
                    $"{ForgeNumbers.Inventory.DossierGridSide} storage hooks installed");
        }
        catch (Exception ex)
        {
            log($"[WARN] [NicokoboForge/Dossier] expansion disabled: {ex.Message}");
        }
    }

    private static void AfterCreate(GameItem __result) => Expand(__result);

    private static void BeforeLoad()
    {
        if (!Installed) return;
        try
        {
            // The scene owns this fixture before LoadGame decodes its contents.
            // Expand before placement so saved items beyond the old 10x10 fit.
            var scene = EmporiumEntry.Instance;
            if (scene != null && scene.Pointer != IntPtr.Zero) Expand(scene.dossier);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[WARN] [NicokoboForge/Dossier] load expansion failed: {ex.Message}");
        }
    }

    private static void Expand(GameItem? dossier)
    {
        if (!Installed || dossier == null || dossier.Pointer == IntPtr.Zero) return;
        try
        {
            var window = dossier.contentWindow;
            var grid = window?.childElement?.TryCast<GameGridInventory>();
            if (window == null || window.Pointer == IntPtr.Zero || grid == null || grid.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native dossier storage grid is unavailable");
            var shape = grid.inventoryShape;
            if (shape == null || shape.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native dossier storage shape is unavailable");
            int side = ForgeNumbers.Inventory.DossierGridSide;
            if (shape.width == side && shape.height == side) return;
            if (shape.width > side || shape.height > side)
                throw new InvalidOperationException("Dossier already exceeds the target size; refusing to shrink its storage");

            // Resize the existing grid in place: retain items, placement shapes,
            // parent links and the native document/tool admission callbacks.
            grid.SetShape(side, side);
            window.Validate();
            if (grid.inventoryShape.width != side || grid.inventoryShape.height != side)
                throw new InvalidOperationException("Native dossier storage size readback failed");
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[WARN] [NicokoboForge/Dossier] expansion failed: {ex.Message}");
        }
    }
}
