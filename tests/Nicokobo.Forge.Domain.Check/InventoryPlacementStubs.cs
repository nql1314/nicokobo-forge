// Detached geometry and native-slot fault ports for the production preview API.
// The fixtures create no IL2CPP objects or detours.
namespace Il2Cpp;

public sealed partial class GridShape
{
    public GridShape(int width, int height, params byte[] cells) : this(0)
    { Width = width; Height = height; Cells = cells; }
    public int Width = 1, Height = 1, X, Y;
    public byte[] Cells = [1];
    public byte Outside;
    public int globalWidth => Width;
    public int globalHeight => Height;
    public int minX => X;
    public int minY => Y;
    public int maxX => X + Width - 1;
    public int maxY => Y + Height - 1;
    public byte GetOutsideBounds() => Outside;
    public byte Get(int x, int y) => x < minX || x > maxX || y < minY || y > maxY
        ? Outside : Cells[(y - Y) * Width + x - X];
    public GridShape Clone() => new(Width, Height, (byte[])Cells.Clone())
        { X = X, Y = Y, Outside = Outside };
}

public sealed class GridShapeBuilder(GridShape source)
{
    public GridShape shape { get; private set; } = source.Clone();
    public GridShapeBuilder SetTransform(int x, int y, bool flipped, int turns)
    {
        int width = source.Width, height = source.Height;
        var cells = (byte[])source.Cells.Clone();
        if (flipped)
            for (int row = 0; row < height; row++) Array.Reverse(cells, row * width, width);
        for (int turn = 0; turn < turns % 4; turn++)
        {
            var rotated = new byte[cells.Length];
            for (int cy = 0; cy < height; cy++)
                for (int cx = 0; cx < width; cx++)
                    rotated[cx * height + height - cy - 1] = cells[cy * width + cx];
            cells = rotated;
            (width, height) = (height, width);
        }
        shape = new(width, height, cells) { X = x, Y = y, Outside = source.Outside };
        return this;
    }
    public GridShapeBuilder SetPosition(int x, int y) { shape.X = x; shape.Y = y; return this; }
    public GridShape Clone() => shape.Clone();
}

public sealed partial class GameItem
{
    public GridShape shape = new(1, 1, 1);
}

public partial class GameInventory
{
    public bool PlacementFixture, RejectPlacementPreview, MergePlacementPreview;
    public int PreviewCalls;
    public Action? DuringPlacementPreview;
    public Func<SlotMarker, SlotMarker>? PlacementSlotOverride;
    public SlotMarker? LastPlacementSlot;
    private SlotMarker? FindPlacementSlot(GameItem item, int maximum, GridShape shape)
    {
        PreviewCalls++;
        DuringPlacementPreview?.Invoke();
        if (IsInsertLocked() || RejectPlacementPreview || mayInventoryAddItemFunc?.Invoke(item) == false) return null;
        var slot = new SlotMarker { item = item, inventory = this as GameGridInventory,
            targetItem = MergePlacementPreview ? new GameItem() : null, numTransfer = maximum };
        return LastPlacementSlot = PlacementSlotOverride?.Invoke(slot) ?? slot;
    }
}

public sealed partial class GameGridInventory
{
    public GridShape inventoryShape = new(1, 1, 0);
}
