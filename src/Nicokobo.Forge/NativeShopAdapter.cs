using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class NativeShopAdapter
{
    private sealed record NightShopRefresh(string ItemId, int PurchasedUniqueId,
        int Attempts, DateTime NotBeforeUtc);
    private static readonly object Gate = new();
    private static Action<string>? _log;
    private static bool _allowed, _nightShopEnabled;
    private static NightShopRefresh? _pendingNightShopRefresh;
    internal static bool Installed => _nightShopEnabled;
    internal static void Configure(bool allowed, Action<string> log)
    { _allowed = allowed; _log = log; DeclarationsChanged(); }
    internal static void DeclarationsChanged()
    {
        if (!_allowed || _nightShopEnabled || !NativeItemRegistry.Declarations().Any(i => i.Options.NightShop != NightShopStockPolicy.None)) return;
        _nightShopEnabled = NativeHookSet.Install("nicokobo.forge.night_shop", [
            new(typeof(StoreClientList), nameof(StoreClientList.PlaceInventorInventory), [typeof(bool)], typeof(void), typeof(NativeShopAdapter), Postfix: nameof(NightShopStockPostfix)),
            new(typeof(PlayerStore), nameof(PlayerStore.OnItemBought), [typeof(GameItem), typeof(int)], typeof(void), typeof(NativeShopAdapter), Postfix: nameof(NightShopPurchasePostfix))], _log);
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { NightShopStock = _nightShopEnabled });
    }
    private static void NightShopStockPostfix(bool isVisitingPlayerStore)
    {
        if (!_nightShopEnabled || isVisitingPlayerStore) return;
        try
        {
            var store = PlayerStore.Instance;
            var inventory = EmporiumEntry.Instance?.frontInvinvElement;
            if (store == null || store.Pointer == IntPtr.Zero || inventory == null ||
                inventory.Pointer == IntPtr.Zero || inventory.childItems == null)
                throw new InvalidOperationException("Night-shop inventory is unavailable");

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < inventory.childItems.Count; index++)
            {
                var item = inventory.childItems[index];
                if (item != null && item.Pointer != IntPtr.Zero &&
                    !string.IsNullOrWhiteSpace(item.identifier))
                    present.Add(item.identifier);
            }

            NativeItemDeclaration[] offers;
            lock (Gate)
                offers = NativeItemRegistry.Declarations().Where(item =>
                    item.Options.NightShop != NightShopStockPolicy.None).ToArray();
            int added = 0;
            foreach (var offer in offers)
            {
                if (present.Contains(offer.ItemId) ||
                    offer.Options.NightShop == NightShopStockPolicy.Unique &&
                    store.IsPlayerOwnThisItem(offer.ItemId))
                    continue;
                bool available;
                try { available = offer.Options.IsNightShopAvailable?.Invoke() ?? true; }
                catch (Exception ex)
                {
                    SafeLog(_log, $"[ERROR] [NicokoboForge/NightShop] owner={offer.OwnerId}; " +
                        $"id={offer.ItemId}; status=AvailabilityFailed; " +
                        $"reason={ex.GetType().Name}: {ex.Message}");
                    continue;
                }
                if (!available) continue;

                var status = NativeItemRegistry.Outcome(offer.ItemId);
                if (status != NativeApplicationStatus.Applied) continue;

                GameItem? item = null;
                try
                {
                    item = DirectoryMaster.Item(offer.ItemId, true);
                    if (item == null || item.Pointer == IntPtr.Zero ||
                        item.identifier != offer.ItemId)
                        throw new InvalidOperationException("Registered item could not be created");
                    store.AddDirectSellingItemToTable(item, false, false, false, 0);
                    bool accepted = false;
                    for (int index = 0; index < inventory.childItems.Count; index++)
                    {
                        var placed = inventory.childItems[index];
                        if (placed != null && placed.Pointer == item.Pointer)
                        {
                            accepted = true;
                            break;
                        }
                    }
                    if (!accepted)
                        throw new InvalidOperationException("Night-shop inventory rejected item");
                    present.Add(offer.ItemId);
                    added++;
                }
                catch (Exception ex)
                {
                    if (item != null && item.Pointer != IntPtr.Zero &&
                        item.parentInventory == null)
                        item.Destroy();
                    SafeLog(_log, $"[ERROR] [NicokoboForge/NightShop] owner={offer.OwnerId}; " +
                        $"id={offer.ItemId}; status=Failed; " +
                        $"reason={ex.GetType().Name}: {ex.Message}");
                }
            }
            if (added != 0)
                SafeLog(_log, $"[NicokoboForge/NightShop] status=Applied; added={added}");
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/NightShop] status=Failed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void NightShopPurchasePostfix(GameItem __0)
    {
        if (!_nightShopEnabled || __0 == null || __0.Pointer == IntPtr.Zero) return;
        NativeItemDeclaration? offer;
        lock (Gate)
        {
            if ((offer = NativeItemRegistry.Declarations().FirstOrDefault(x => x.ItemId == __0.identifier)) == null ||
                offer?.Options.NightShop != NightShopStockPolicy.Repeatable) return;
            _pendingNightShopRefresh = new(offer.ItemId, __0.uniqueId, 0,
                DateTime.UtcNow.AddMilliseconds(100));
        }
        SafeLog(_log, $"[NicokoboForge/NightShop] id={offer.ItemId}; " +
            $"status=RefreshQueued; purchased={__0.uniqueId}");
    }

    internal static void Update()
    {
        NightShopRefresh? pending;
        lock (Gate) pending = _pendingNightShopRefresh;
        if (!_nightShopEnabled || pending == null ||
            DateTime.UtcNow < pending.NotBeforeUtc) return;
        try
        {
            if (HasReplacementStock(pending))
            {
                ClearPendingRefresh(pending, "AlreadyAvailable");
                return;
            }
            NightShopStockPostfix(false);
            if (HasReplacementStock(pending))
            {
                ClearPendingRefresh(pending, "Applied");
                return;
            }
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[NicokoboForge/NightShop] id={pending.ItemId}; " +
                $"status=RefreshDeferred; reason={ex.GetType().Name}: {ex.Message}");
        }
        lock (Gate)
        {
            if (_pendingNightShopRefresh != pending) return;
            int attempts = pending.Attempts + 1;
            if (attempts >= 40)
            {
                _pendingNightShopRefresh = null;
                SafeLog(_log, $"[WARN] [NicokoboForge/NightShop] id={pending.ItemId}; " +
                    "status=RefreshFailed; reason=no free accepted replacement after 40 attempts");
                return;
            }
            _pendingNightShopRefresh = pending with
            {
                Attempts = attempts,
                NotBeforeUtc = DateTime.UtcNow.AddMilliseconds(250)
            };
        }
    }

    private static bool HasReplacementStock(NightShopRefresh pending)
    {
        var items = EmporiumEntry.Instance?.frontInvinvElement?.childItems;
        if (items == null) return false;
        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item != null && item.Pointer != IntPtr.Zero &&
                item.identifier == pending.ItemId &&
                item.uniqueId != pending.PurchasedUniqueId)
                return true;
        }
        return false;
    }

    private static void ClearPendingRefresh(NightShopRefresh pending, string status)
    {
        lock (Gate)
        {
            if (_pendingNightShopRefresh != pending) return;
            _pendingNightShopRefresh = null;
        }
        SafeLog(_log, $"[NicokoboForge/NightShop] id={pending.ItemId}; " +
            $"status=Refresh{status}; attempts={pending.Attempts}");
    }

    private static void SafeLog(Action<string>? log, string message)
    { try { log?.Invoke(message); } catch { } }
}
