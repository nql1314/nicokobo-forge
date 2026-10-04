using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

internal static class NightShopChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool condition, string message)
        { checks++; if (!condition) throw new Exception("Night shop: " + message); }
        var logs = new List<string>();
        var created = new List<GameItem>();
        const string repeatable = "test.shop.machine", unique = "test.shop.guide";
        void Offer(string id, NightShopStockPolicy policy, int space = 1, int units = 1,
            Func<bool>? available = null)
        {
            Func<GameItem> factory = () =>
            {
                var item = new GameItem { Pointer = new(100 + created.Count), identifier = id,
                    ShelfSpace = space, unitCount = units, Owned = true };
                created.Add(item);
                return item;
            };
            NativeItemRegistry.Offers.Add(new("test.shop", id, NativeItemKind.Item, factory,
                new(policy, available)));
        }
        GameGridInventory Reset()
        {
            NativeItemRegistry.Offers.Clear();
            NativeItemRegistry.Outcomes.Clear();
            created.Clear(); logs.Clear();
            PlayerStore.instance = new();
            EmporiumEntry.Instance = new();
            return EmporiumEntry.Instance.frontInvinvElement;
        }

        var shelf = Reset();
        Offer(repeatable, NightShopStockPolicy.Repeatable);
        NativeShopAdapter.Configure(true, logs.Add);
        StoreClientList.PlaceInventorInventory(true);
        Expect(created.Count == 0 && shelf.childItems.Count == 0,
            "Visiting inventor added night-shop stock to the daytime counter");
        var daytimeItem = new GameItem { identifier = repeatable };
        PlayerStore.instance!.OnItemBought(daytimeItem, 10);
        Expect(created.Count == 0 && shelf.childItems.Count == 0,
            "Daytime purchase injected inventor stock");

        StoreClientList.PlaceInventorInventory(false);
        Expect(shelf.childItems.Count == 1 && !shelf.childItems[0].Owned,
            "Night-shop generation did not place an unowned registered item");
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 1, "Existing stock was duplicated");
        var bought = shelf.childItems[0];
        PlayerStore.instance.OnItemBought(bought, 10);
        Expect(created.Count == 1 && shelf.childItems.Count == 0 && bought.Owned,
            "Night-shop purchase replenished stock or changed the purchased item");
        StoreClientList.PlaceInventorInventory(true);
        PlayerStore.instance.OnItemBought(new GameItem { identifier = repeatable }, 10);
        Expect(created.Count == 1 && shelf.childItems.Count == 0,
            "A later daytime visit resumed night-shop replenishment");
        StoreClientList.PlaceInventorInventory(false);
        Expect(shelf.childItems.Count == 1 && created.Count == 2,
            "Next native night-shop generation lost repeatable availability");

        shelf = Reset(); shelf.Capacity = 2;
        Offer("test.shop.large", NightShopStockPolicy.Repeatable, space: 3);
        Offer("test.shop.small", NightShopStockPolicy.Repeatable);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created[0].DestroyCalls == 1 && PlayerStore.instance!.Placements == 1,
            "An oversized offer reached native rejection or leaked its detached item");
        Expect(shelf.childItems.Single().identifier == "test.shop.small",
            "An oversized offer prevented a later fitting item from being placed");
        Expect(logs.Any(x => x.Contains("status=Skipped")) && !logs.Any(x => x.Contains("[ERROR]")),
            "Ordinary shelf capacity was reported as an error");

        Reset(); PlayerStore.instance!.RejectPlacement = true;
        Offer(repeatable, NightShopStockPolicy.Repeatable);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Single().DestroyCalls == 1,
            "Native rejection was followed by a second Destroy");
        Expect(logs.Any(x => x.Contains("RejectedByNativePlacement")) &&
            !logs.Any(x => x.Contains("status=Applied")), "Native rejection was reported as applied");
        Reset(); PlayerStore.instance!.RejectPlacement = true;
        PlayerStore.instance.ThrowAfterRejection = true;
        Offer(repeatable, NightShopStockPolicy.Repeatable);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Single().DestroyCalls == 1 && logs.Any(x => x.Contains("status=Failed")),
            "Exception after native cleanup caused a second Destroy or hid failure");

        shelf = Reset(); shelf.ThrowOnPreflight = true;
        Offer(repeatable, NightShopStockPolicy.Repeatable);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Single().DestroyCalls == 1 && PlayerStore.instance!.Placements == 0,
            "Preflight exception leaked or submitted a detached item");
        shelf = Reset(); shelf.SlotUnits = 1;
        Offer(repeatable, NightShopStockPolicy.Repeatable, units: 2);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Single().DestroyCalls == 1 && PlayerStore.instance!.Placements == 0,
            "Partial capacity reached all-or-destroy native placement");

        shelf = Reset();
        Offer(unique, NightShopStockPolicy.Unique);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 1 && shelf.childItems.Single().identifier == unique,
            "A missing guide was not offered by native night-shop generation");
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 1 && shelf.childItems.Count == 1,
            "A guide already on the shelf was duplicated");
        bought = shelf.childItems[0];
        PlayerStore.instance!.OnItemBought(bought, 10);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 1 && shelf.childItems.Count == 0,
            "An owned guide returned on a later native night-shop generation");
        PlayerStore.instance.OwnedIds.Remove(unique);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 2 && shelf.childItems.Single().identifier == unique,
            "Losing the last guide did not restore its night-shop availability");

        shelf = Reset();
        Offer(unique, NightShopStockPolicy.Unique);
        PlayerStore.instance!.OwnedIds.Add(unique);
        Offer("test.shop.none", NightShopStockPolicy.None);
        Offer("test.shop.staged", NightShopStockPolicy.Repeatable);
        NativeItemRegistry.Outcomes["test.shop.staged"] = NativeApplicationStatus.Staged;
        Offer("test.shop.unavailable", NightShopStockPolicy.Repeatable, available: () => false);
        StoreClientList.PlaceInventorInventory(false);
        Expect(created.Count == 0 && shelf.childItems.Count == 0,
            "Ownership, disabled policy, staged factory or availability filtering failed");
        PlayerStore.instance = null;
        StoreClientList.PlaceInventorInventory(false);
        Expect(PlayerStore.SingletonCreations == 0, "Stock hook constructed a store singleton");
        Console.WriteLine($"Night shop: {checks} assertions passed (purchase isolation, capacity, cleanup, availability).");
    }
}
