using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class NativeShopAdapter
{
    private static Action<string>? _log;
    private static bool _allowed, _nightShopEnabled;
    internal static bool Installed => _nightShopEnabled;
    internal static void Configure(bool allowed, Action<string> log)
    { _allowed = allowed; _log = log; DeclarationsChanged(); }
    internal static void DeclarationsChanged()
    {
        if (!_allowed || _nightShopEnabled || !NativeItemRegistry.Declarations().Any(i => i.Options.NightShop != NightShopStockPolicy.None)) return;
        _nightShopEnabled = NativeHookSet.Install("nicokobo.forge.night_shop", [
            new(typeof(StoreClientList), nameof(StoreClientList.PlaceInventorInventory), [typeof(bool)], typeof(void), typeof(NativeShopAdapter), Postfix: nameof(NightShopStockPostfix))], _log);
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { NightShopStock = _nightShopEnabled });
    }
    private static void NightShopStockPostfix(bool isVisitingPlayerStore)
    {
        if (!_nightShopEnabled || isVisitingPlayerStore) return;
        try
        {
            var store = PlayerStore.instance;
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

            var offers = NativeItemRegistry.Declarations().Where(item =>
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
                bool submitted = false;
                try
                {
                    item = DirectoryMaster.Item(offer.ItemId, true);
                    if (item == null || item.Pointer == IntPtr.Zero ||
                        item.identifier != offer.ItemId || item.parentInventory != null ||
                        item.unitCount <= 0)
                        throw new InvalidOperationException("Registered item could not be created");
                    // Match native selling ownership before checking the remaining
                    // shelf space. A full shelf is normal, and creates no retry job.
                    GeneralHelper.SetItemOwned(item, false);
                    var slot = inventory.TryFindOneValidInventorySlot(item, false);
                    if (slot == null || slot.Pointer == IntPtr.Zero ||
                        !slot.IsValid() || slot.numTransfer < item.unitCount)
                    {
                        item.Destroy();
                        item = null;
                        SafeLog(_log, $"[NicokoboForge/NightShop] owner={offer.OwnerId}; " +
                            $"id={offer.ItemId}; status=Skipped; reason=no room on shelf");
                        continue;
                    }
                    // AddDirectSellingItemToTable destroys rejected detached items.
                    // After submission the native placement owns their cleanup.
                    submitted = true;
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
                    {
                        SafeLog(_log, $"[WARN] [NicokoboForge/NightShop] owner={offer.OwnerId}; " +
                            $"id={offer.ItemId}; status=RejectedByNativePlacement");
                        continue;
                    }
                    present.Add(offer.ItemId);
                    added++;
                }
                catch (Exception ex)
                {
                    if (!submitted && item != null && item.Pointer != IntPtr.Zero &&
                        item.parentInventory == null)
                        try { item.Destroy(); } catch { }
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

    private static void SafeLog(Action<string>? log, string message)
    { try { log?.Invoke(message); } catch { } }
}
