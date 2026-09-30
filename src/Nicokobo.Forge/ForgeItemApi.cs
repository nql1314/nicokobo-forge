using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

public sealed record NativeModuleRegistration(string ItemId, Func<GameItem> Factory, NativeItemOptions? Options = null);

/// <summary>Content identity, factories and observed directory application.
/// Distribution, presentation and native hooks belong to separate adapters.</summary>
public static class ForgeItemApi
{
    public static SubmitResult RegisterNode(string ownerId, string itemId, Func<GameItem> factory, NativeItemOptions? options = null) => NativeItemRegistry.RegisterNode(ownerId, itemId, factory, options);
    public static SubmitResult RegisterItem(string ownerId, string itemId, Func<GameItem> factory, NativeItemOptions? options = null) => NativeItemRegistry.RegisterItem(ownerId, itemId, factory, options);
    public static SubmitResult RegisterAmenity(string ownerId, string itemId, Func<GameItem> factory, NativeItemOptions? options = null) => NativeItemRegistry.RegisterAmenity(ownerId, itemId, factory, options);
    public static SubmitResult RegisterModule(string ownerId, string itemId, Func<GameItem> factory, NativeItemOptions? options = null) => NativeItemRegistry.RegisterModule(ownerId, itemId, factory, options);
    public static SubmitResult RegisterModules(string ownerId, IReadOnlyList<NativeModuleRegistration> modules) => NativeItemRegistry.RegisterModules(ownerId, modules);
    public static IReadOnlyList<NativeApplicationView> Snapshot() => NativeItemRegistry.Snapshot();
    public static bool Exists(string itemId) => ForgeCapabilities.Current.KnownGameBuild && DirectoryMaster.Has<GameItem>(itemId);
    /// <summary>Creates a native item or template instance. The caller owns the
    /// returned handle and must place or destroy it. This does not register IDs.</summary>
    public static GameItem Create(string itemId, bool owned = true)
    {
        if (!ForgeCapabilities.Current.KnownGameBuild || string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("Item creation adapter unavailable");
        var item = DirectoryMaster.Item(itemId, owned);
        return item != null && item.Pointer != IntPtr.Zero && item.identifier == itemId
            ? item : throw new InvalidOperationException("Native item factory returned an invalid item: " + itemId);
    }
    public static string GetDisplayName(string itemId)
    {
        var item = Create(itemId, false);
        try { return item.GetDisplayName(false); }
        finally { if (item.parentInventory == null) item.Destroy(); }
    }
}
