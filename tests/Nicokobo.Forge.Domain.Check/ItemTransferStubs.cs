namespace Il2Cpp
{
    public sealed record GridShape(int Position);
    public sealed partial class GameItem
    {
        public GridShape modifiedShape = new(0);
        public bool MayRemove() => true;
    }
    public partial class GameInventory
    {
        public List<GameItem> childItems = [];
        public Func<GameItem, bool>? mayInventoryAddItemFunc;
        public Action<GameItem>? onSlotAddItemFunc;
        public bool overrideLockInsert, overrideLockRemove, RefuseAccept, RefuseExpel, ThrowAfterAttach;
        public bool IsInsertLocked() => overrideLockInsert;
        public bool IsRemoveLocked() => overrideLockRemove;
        public SlotMarker? TryInventorySlot(GameItem item, int maximum, GridShape shape)
        {
            if (IsInsertLocked() || mayInventoryAddItemFunc?.Invoke(item) == false ||
                childItems.Any(i => i.modifiedShape.Position == shape.Position)) return null;
            return new() { item = item, inventory = this as GameGridInventory, numTransfer = maximum };
        }
        public bool Expel(GameItem item)
        {
            if (RefuseExpel || IsRemoveLocked()) return false;
            childItems.Remove(item); item.parentInventory = null; return true;
        }
        public bool UncheckedAccept(GameItem item)
        {
            if (RefuseAccept || item.parentInventory != null || childItems.Contains(item)) return false;
            onSlotAddItemFunc?.Invoke(item);
            childItems.Add(item); item.parentInventory = this;
            if (ThrowAfterAttach) throw new InvalidOperationException("Native postfix failed after attach");
            return true;
        }
    }
    public sealed partial class GameGridInventory
    {
        public bool TransferFixture, MergeTransfers;
        public SlotMarker? LastTransfer;
        private SlotMarker? FindTransferSlot(GameItem item)
        {
            if (IsInsertLocked() || childItems.Count >= Capacity) return null;
            return LastTransfer = new() { item = item, inventory = this,
                targetItem = MergeTransfers ? childItems.FirstOrDefault(i => i.identifier == item.identifier) : null,
                numTransfer = Math.Min(SlotUnits, item.unitCount) };
        }
    }
    public sealed partial class SlotMarker
    {
        public GameItem? item, targetItem;
        public GameGridInventory? inventory;
        public int TryAcceptOnce(int maximum)
        {
            var original = item!;
            int amount = Math.Min(maximum, numTransfer);
            if (targetItem != null)
            {
                targetItem.SetUnitCount(targetItem.unitCount + amount);
                if (amount == original.unitCount) { original.parentInventory!.Expel(original); original.Destroy(); }
                else original.SetUnitCount(original.unitCount - amount);
                return amount;
            }
            if (amount == original.unitCount) original.parentInventory!.Expel(original);
            else
            {
                original.SetUnitCount(original.unitCount - amount);
                item = new GameItem { Pointer = new(201), identifier = original.identifier,
                    Owned = original.Owned, unitCount = amount, modifiedShape = original.modifiedShape };
            }
            item!.modifiedShape = new(99);
            return inventory!.UncheckedAccept(item) ? amount : 0;
        }
    }
}
