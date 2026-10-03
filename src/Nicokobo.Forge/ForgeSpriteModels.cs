namespace Nicokobo.Forge;

/// <summary>Embedded artwork remains in the content assembly. Dimensions are
/// decoded pixels, independent of the item's inventory footprint.</summary>
public sealed record ForgeSpriteDefinition(string Key, string ResourceName, int Width, int Height);

public enum ForgeSpriteFilter { Point, Bilinear }
