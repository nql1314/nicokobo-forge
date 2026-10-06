using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Workshop;

// The production commit entry is linked directly. Native slot lookup can return
// stale identities even after a successful whole-batch geometry preview.
internal static class WorkshopDeliveryChecks
{
    internal static void Run()
    {
        int checks = 0;
        var failures = new List<string>();
        void Expect(bool value, string message)
        { checks++; if (!value) failures.Add(message); }
        (GameGridInventory Target, GameItem Item, ForgeInventoryPlacement Placement) Setup()
        {
            var target = new GameGridInventory { Pointer = new(1001), PlacementFixture = true };
            var item = new GameItem { Pointer = new(1002), identifier = "reward", Owned = true };
            return (target, item, new(item, new(1, 1, 1)));
        }
        {
            var (target, item, placement) = Setup();
            Expect(WorkshopRewardDelivery.Apply(target, [placement]) && item.parentInventory == target &&
                target.childItems.Single() == item && target.LastPlacementSlot!.AcceptCalls == 1,
                "Valid detached reward did not commit through native acceptance");
        }
        foreach (string fault in new[] { "item", "destination", "count", "pointer" })
        {
            var (target, item, placement) = Setup();
            var otherInventory = new GameGridInventory { Pointer = new(1003) };
            var otherItem = new GameItem { Pointer = new(1004), identifier = item.identifier,
                unitCount = 1, Owned = true, parentInventory = otherInventory };
            otherInventory.childItems.Add(otherItem);
            target.PlacementSlotOverride = slot =>
            {
                if (fault == "item") slot.item = otherItem;
                if (fault == "destination") slot.inventory = otherInventory;
                if (fault == "count") slot.numTransfer = 2;
                if (fault == "pointer") slot.Pointer = IntPtr.Zero;
                return slot;
            };
            bool applied = WorkshopRewardDelivery.Apply(target, [placement]);
            Expect(!applied && target.LastPlacementSlot!.AcceptCalls == 0 && target.childItems.Count == 0 &&
                item.parentInventory == null && otherItem.parentInventory == otherInventory &&
                otherInventory.childItems.Count == 1,
                $"Stale {fault} slot mutated an unrelated item or destination before failing");
        }
        {
            var (target, item, placement) = Setup();
            var otherInventory = new GameGridInventory { Pointer = new(1003) };
            target.DuringPlacementPreview = () =>
            { otherInventory.childItems.Add(item); item.parentInventory = otherInventory; };
            Expect(!WorkshopRewardDelivery.Apply(target, [placement]) &&
                target.LastPlacementSlot!.AcceptCalls == 0 && item.parentInventory == otherInventory &&
                target.childItems.Count == 0,
                "An admission callback's new parent was overwritten by reward commit");
        }
        {
            var (target, item, placement) = Setup();
            item.unitCount = 2;
            Expect(!WorkshopRewardDelivery.Apply(target, [placement]) && target.PreviewCalls == 0 &&
                target.childItems.Count == 0 && item.unitCount == 2,
                "Changed reward quantity was split by the commit path");
        }
        if (failures.Count > 0) throw new Exception("Workshop delivery: " + string.Join("; ", failures));
        Console.WriteLine($"Workshop delivery: {checks} offline checks passed");
    }
}
