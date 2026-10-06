using Il2Cpp;
using Nicokobo.Forge;

// Offline fault injection follows Build 25382790 SlotMarker ordering:
// expel/split, transform, destination callback, then attachment. The production
// ForgeItemTransferApi is linked into this check; no game process is started.
internal static class ItemTransferChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Item transfer: " + message); }
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { KnownGameBuild = true });
        (GameGridInventory Source, GameGridInventory Target, GameItem Item) Setup()
        {
            var source = new GameGridInventory { Pointer = new(101), TransferFixture = true };
            var target = new GameGridInventory { Pointer = new(102), TransferFixture = true };
            var item = new GameItem { Pointer = new(103), identifier = "ore", unitCount = 9,
                Owned = true, parentInventory = source, modifiedShape = new(7) };
            source.childItems.Add(item);
            return (source, target, item);
        }
        foreach (int requested in new[] { 9, 4 })
        {
            var (source, target, item) = Setup();
            var result = ForgeItemTransferApi.Move(item, target, requested);
            Expect(result.Moved == requested && !result.Indeterminate &&
                source.childItems.Sum(i => i.unitCount) == 9 - requested &&
                target.childItems.Sum(i => i.unitCount) == requested,
                $"Normal {requested}-unit transfer did not conserve quantity");
        }
        {
            var (source, target, item) = Setup();
            item.MaximumRemoval = 2;
            var result = ForgeItemTransferApi.Move(item, target, 9);
            Expect(result.Moved == 2 && !result.Indeterminate && item.unitCount == 7,
                "Native MaxNumRemove cap was bypassed or mistaken for the requested quantity");
            item.MaximumRemoval = 0;
            Expect(ForgeItemTransferApi.Move(item, target, 9).Status == "locked-or-source",
                "A source with no removable units was detached");
        }
        {
            var (source, target, item) = Setup();
            var other = new GameItem { Pointer = new(104), identifier = "ore", unitCount = 1,
                Owned = true, parentInventory = source, modifiedShape = new(8) };
            source.childItems.Add(other);
            target.TransferItemOverride = other;
            var result = ForgeItemTransferApi.Move(item, target, 9);
            Expect(result.Moved == 0 && !result.Indeterminate && source.childItems.Count == 2 &&
                other.parentInventory == source && item.parentInventory == source && target.childItems.Count == 0,
                "A stale slot for another same-ID item moved that item instead of the requested instance");
        }
        {
            var (source, target, item) = Setup();
            var third = new GameGridInventory { Pointer = new(105) };
            target.TransferDestinationOverride = third;
            var result = ForgeItemTransferApi.Move(item, target, 9);
            Expect(result.Moved == 0 && !result.Indeterminate && item.parentInventory == source && third.childItems.Count == 0,
                "A stale slot for another destination detached the source");
        }
        foreach (int requested in new[] { 9, 4 })
        foreach (bool throws in new[] { false, true })
        {
            var (source, target, item) = Setup();
            var originalShape = item.modifiedShape;
            Func<GameItem, bool> admission = _ => false;
            Action<GameItem> sourceCallback = _ => throw new Exception("Source insert callback must not run during recovery");
            source.mayInventoryAddItemFunc = admission;
            source.onSlotAddItemFunc = sourceCallback;
            source.overrideLockInsert = true;
            target.RefuseAccept = !throws;
            if (throws) target.onSlotAddItemFunc = _ => throw new InvalidOperationException("Destination callback failed");
            var result = ForgeItemTransferApi.Move(item, target, requested);
            Expect(result.Moved == 0 && !result.Indeterminate && result.Status == "rejected-restored" &&
                source.childItems.Single().Pointer == item.Pointer && item.unitCount == 9 &&
                target.childItems.Count == 0 && item.parentInventory == source && item.modifiedShape == originalShape,
                $"Rejected/thrown {requested}-unit move lost its original source quantity, identity or placement");
            Expect(source.mayInventoryAddItemFunc == admission && source.onSlotAddItemFunc == sourceCallback &&
                source.overrideLockInsert, "Recovery did not restore source admission/callback/lock");
            if (requested < 9)
                Expect(target.LastTransfer!.item!.DestroyCalls == 1,
                    "A recombined split was left alive or destroyed twice");
        }
        foreach (int requested in new[] { 9, 4 })
        {
            var (source, target, item) = Setup();
            target.ThrowAfterAttach = true;
            var result = ForgeItemTransferApi.Move(item, target, requested);
            Expect(result.Moved == requested && !result.Indeterminate &&
                source.childItems.Sum(i => i.unitCount) + target.childItems.Sum(i => i.unitCount) == 9,
                "An attached move was refunded after a callback exception");
        }
        {
            var (source, target, item) = Setup();
            var blocker = new GameItem { Pointer = new(106), identifier = "other", unitCount = 1,
                Owned = true, parentInventory = source, modifiedShape = item.modifiedShape };
            target.onSlotAddItemFunc = _ =>
            {
                source.childItems.Add(blocker);
                throw new InvalidOperationException("Source position was occupied during destination acceptance");
            };
            var result = ForgeItemTransferApi.Move(item, target, 9);
            Expect(result.Moved == 0 && result.Indeterminate && item.parentInventory == null &&
                source.childItems.Single().Pointer == blocker.Pointer && target.childItems.Count == 0,
                "Recovery overwrote the newly occupied source position or hid a detached item");
        }
        foreach (int requested in new[] { 9, 4 })
        {
            var (source, target, item) = Setup();
            var receiver = new GameItem { Pointer = new(104), identifier = "ore", unitCount = 2, parentInventory = target };
            target.childItems.Add(receiver); target.MergeTransfers = true;
            var result = ForgeItemTransferApi.Move(item, target, requested);
            Expect(result.Moved == requested && !result.Indeterminate && receiver.unitCount == 2 + requested &&
                source.childItems.Sum(i => i.unitCount) == 9 - requested,
                "Stack merging read back a consumed source handle or duplicated units");
        }
        {
            var (source, target, item) = Setup();
            var shape = item.modifiedShape;
            source.RefuseExpel = true;
            var result = ForgeItemTransferApi.Move(item, target, 9);
            Expect(result.Moved == 0 && !result.Indeterminate && item.modifiedShape == shape && item.parentInventory == source,
                "Expel refusal left a destination transform on the source item");
            source.overrideLockRemove = true;
            Expect(ForgeItemTransferApi.Move(item, target, 9).Status == "locked-or-source", "Source removal lock was bypassed");
        }
        Console.WriteLine($"Item transfers: {checks} offline checks passed");
    }
}
