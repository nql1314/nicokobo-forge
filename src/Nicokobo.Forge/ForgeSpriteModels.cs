namespace Nicokobo.Forge;

/// <summary>Embedded artwork remains in the content assembly. Dimensions are
/// decoded pixels, independent of the item's inventory footprint.</summary>
public sealed record ForgeSpriteDefinition(string Key, string ResourceName, int Width, int Height)
{
    public float PivotX { get; init; }
    public float PivotY { get; init; } = 1;
    // Inventory alpha hit testing requires readable pixels. Display-only art
    // can release its CPU pixel buffer after decoding.
    public bool KeepReadable { get; init; } = true;
}

public enum ForgeSpriteFilter { Point, Bilinear }
