namespace Nicokobo.Forge.Workshop;

internal readonly record struct WorkshopGridCell(int X, int Y);

// Only this detached bitmap changes during batch preflight.
internal sealed class WorkshopGridPlanner
{
    private readonly int _width, _height;
    private readonly bool[] _blocked;

    internal WorkshopGridPlanner(int width, int height, ReadOnlySpan<bool> blocked)
    {
        if (width <= 0 || height <= 0 || width > 64 || height > 64 || blocked.Length != width * height)
            throw new ArgumentException("Invalid reward inventory bitmap");
        _width = width; _height = height; _blocked = blocked.ToArray();
    }

    internal bool TryReserve(IReadOnlyList<WorkshopGridCell> footprint, out WorkshopGridCell position)
    {
        // Native placement scans rows from the top left. Only occupied footprint
        // cells must fit; empty borders can extend beyond the inventory.
        for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                bool fits = true;
                foreach (var cell in footprint)
                {
                    int px = x + cell.X, py = y + cell.Y;
                    if ((uint)px >= _width || (uint)py >= _height || _blocked[py * _width + px])
                    { fits = false; break; }
                }
                if (!fits) continue;
                foreach (var cell in footprint) _blocked[(y + cell.Y) * _width + x + cell.X] = true;
                position = new(x, y);
                return true;
            }
        position = default;
        return false;
    }
}
