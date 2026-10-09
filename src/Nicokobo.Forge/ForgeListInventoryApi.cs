using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>A real native grid used as an internal list backing store. Its
/// geometric layout grows only on insertion; content capacity counts pieces.
/// The content UI shows paged lists and never requires sorting this grid.</summary>
public static class ForgeListInventoryApi
{
    private const string WidthTag = "NICOKOBO_FORGE_LIST_WIDTH", HeightTag = "NICOKOBO_FORGE_LIST_HEIGHT";
    public static bool IsAvailable { get; private set; }
    public static GameGridInventory Create(GameItem owner, string inventoryId)
    {
        if (!IsAvailable || owner == null || owner.Pointer == IntPtr.Zero || owner.parentInventory != null ||
            owner.contentWindow != null || string.IsNullOrWhiteSpace(inventoryId))
            throw new ArgumentException("Detached owner without a window and stable inventory ID required");
        var inventory = new GameGridInventory(16, 16) { identifier = inventoryId };
        var window = new PixelWindow(true, owner.name);
        if (!window.Attach(inventory.Cast<PixelElement>())) throw new InvalidOperationException("Native list window attachment failed");
        owner.SetContentWindow(window);
        if (inventory.GetParentItem()?.Pointer != owner.Pointer) throw new InvalidOperationException("Native list custody attachment failed");
        ContainerHelper.InitContainerItemTagOnly(inventory, owner);
        WriteDimensions(owner, inventory.inventoryShape.width, inventory.inventoryShape.height);
        return inventory;
    }
    public static GameGridInventory? Find(GameItem owner, string inventoryId)
    {
        var inventory = owner.contentWindow?.childElement?.TryCast<GameGridInventory>();
        return inventory?.identifier == inventoryId && inventory.GetParentItem()?.Pointer == owner.Pointer ? inventory : null;
    }
    public static bool IsList(GameGridInventory inventory) => inventory.GetParentItem()?.GetTagReadonly(WidthTag)?.valueInt > 0;
    public static ForgeItemTransferResult Move(GameItem item, GameInventory destination, int maximum,
        Func<GameItem, int, bool> admission)
    {
        var grid = destination?.TryCast<GameGridInventory>();
        if (!IsAvailable || maximum <= 0 || item == null || item.Pointer == IntPtr.Zero || grid == null || !IsList(grid) ||
            grid.IsInsertLocked() || !ForgeInventoryApi.IsPlayerOwned(grid.GetParentItem()) || ForgeInventoryFreezeApi.IsFrozen(item))
            return new(0, false, "list-endpoint-unavailable");
        int requested = Math.Min(maximum, item.unitCount);
        if (requested <= 0 || !admission(item, requested)) return new(0, false, "admission-rejected");
        var seen = new HashSet<IntPtr>();
        for (var parent = grid.GetParentItem(); parent != null; parent = parent.parentInventory?.GetParentItem())
            if (!seen.Add(parent.Pointer) || parent.Pointer == item.Pointer) return new(0, false, "cyclic-custody");
        try
        {
            RestoreDimensions(grid);
            if (grid.TryFindOneValidInventorySlot(item, false) == null)
            {
                int width = Math.Max(grid.inventoryShape.width, Math.Max(item.modifiedShape.width, item.modifiedShape.height));
                // One fresh, completely empty band is sufficient for every
                // legal shape/orientation. A second rejection is admission or
                // a native shape limit, not a reason to allocate forever.
                int height = checked(grid.inventoryShape.height + Math.Max(item.modifiedShape.height, item.modifiedShape.width));
                grid.SetShape(width, height);
                WriteDimensions(grid.GetParentItem(), width, height);
                if (grid.TryFindOneValidInventorySlot(item, false) == null) return new(0, false, "native-shape-or-admission-rejected");
            }
            if (!admission(item, requested)) return new(0, false, "admission-changed");
            return ForgeItemTransferApi.Move(item, grid, requested);
        }
        catch (Exception ex) { return new(0, false, "list-layout-rejected:" + ex.Message); }
    }
    private static void WriteDimensions(GameItem owner, int width, int height)
    {
        foreach (var (name, value) in new[] { (WidthTag, width), (HeightTag, height) })
        { var tag = owner.state.GetTag(name); tag.Enable(); tag.SetInt(value); }
        owner.SyncModifiedState();
    }
    private static void RestoreDimensions(GameGridInventory inventory)
    {
        var owner = inventory.GetParentItem();
        int width = owner?.GetTagReadonly(WidthTag)?.valueInt ?? 0, height = owner?.GetTagReadonly(HeightTag)?.valueInt ?? 0;
        if (width > 0 && height > 0 && (inventory.inventoryShape.width < width || inventory.inventoryShape.height < height))
            inventory.SetShape(Math.Max(width, inventory.inventoryShape.width), Math.Max(height, inventory.inventoryShape.height));
    }
    internal static void Install(bool allowed, Action<string> log)
    {
        if (!allowed) return;
        IsAvailable = NativeHookSet.Install("nicokobo.forge.inventory.list",
        [new(typeof(GameGridInventory), nameof(GameGridInventory.UncheckedAccept), [typeof(GameItem)], typeof(bool), typeof(ForgeListInventoryApi), nameof(BeforeAccept))], log);
    }
    private static void BeforeAccept(GameGridInventory __instance) => RestoreDimensions(__instance);
}
