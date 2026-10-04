namespace Il2Cpp;

// Offline graph fixtures only; they exercise the production lookup during
// construction and replacement without initializing any IL2CPP game object.
public sealed partial class GameItem
{
    public PixelWindow? contentWindow { get; set; }
}

public class PixelElement
{
    public IntPtr Pointer { get; set; } = new(1);
    public T? TryCast<T>() where T : class => this as T;
}

public sealed class PixelWindow : PixelElement
{
    public PixelElement? childElement { get; set; }
}

public sealed class GridPixelElement : PixelElement
{
    public int gridWidth, gridHeight;
    public int Reads;
    public Dictionary<(int X, int Y), PixelElement> Elements { get; } = [];
    public PixelElement? GetElement(int x, int y)
    {
        Reads++;
        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
            throw new InvalidOperationException("Out-of-bounds grid access");
        return Elements.GetValueOrDefault((x, y));
    }
}

public sealed partial class GameGridInventory : PixelElement;
