using Il2Cpp;
using UnityEngine;
using static Nicokobo.Forge.RecipeGuideBrowser;

namespace Nicokobo.Forge;

// Original notebook geometry, with the same item sprites the player sees in
// their inventory. Native sprites are borrowed; the guide never owns them.
internal static class RecipeGuideArtwork
{
    private static readonly Color Edge = new(.35f, .33f, .36f, 1);
    private static readonly Color Blue = new(.29f, .40f, .47f, 1);
    private static readonly Color Worn = new(.58f, .60f, .53f, 1);
    private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.Ordinal);

    private static void Line(Transform p, string name, Vector2 from, Vector2 to, float width, Color color)
    {
        var delta = to - from;
        Box(p, name, (from + to) / 2, new Vector2(delta.magnitude, width), color, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    internal static void Arrow(Transform p, Vector2 at, float width, Color color)
    {
        // A slight kink and an open head match the game's sketched recipe arrows.
        var start = at - new Vector2(width / 2, 2);
        var end = at + new Vector2(width / 2, 0);
        Line(p, "FlowArrowA", start, at + new Vector2(0, 1), 2, color);
        Line(p, "FlowArrowB", at + new Vector2(0, 1), end, 2, color);
        Line(p, "FlowHeadUp", end - new Vector2(12, -8), end, 2, color);
        Line(p, "FlowHeadDown", end - new Vector2(14, 8), end, 2, color);
    }

    internal static void DrawNotebook(Transform p, Color paper)
    {
        Box(p, "PaperBody", Vector2.zero, new Vector2(500, 620), paper);
        Box(p, "PaperTop", new Vector2(-6, 317), new Vector2(484, 14), paper);
        Box(p, "PaperBottom", new Vector2(-6, -317), new Vector2(484, 14), paper);
        Box(p, "PaperTopEdge", new Vector2(-8, 328), new Vector2(480, 2), Edge);
        Box(p, "PaperBottomEdge", new Vector2(-8, -328), new Vector2(480, 2), Edge);
        Box(p, "PaperLeftEdge", new Vector2(-250, 0), new Vector2(2, 652), Edge);
        Box(p, "PaperRightEdge", new Vector2(250, 0), new Vector2(2, 616), Edge);
        for (int i = 0; i < 4; i++)
        {
            float y = 310 + i * 5;
            float x = 249 - i * 5;
            Box(p, "TopCorner:" + i, new Vector2(x, y), new Vector2(2, 5), Edge);
            Box(p, "BottomCorner:" + i, new Vector2(x, -y), new Vector2(2, 5), Edge);
        }
        Box(p, "BindingShade", new Vector2(-239, 0), new Vector2(18, 642), new Color(.25f, .27f, .26f, .09f));
        for (int i = 0; i < 9; i++)
        {
            float y = 270 - i * 66;
            Box(p, "BindingHole:" + i, new Vector2(-235, y), new Vector2(8, 17), Edge);
            Box(p, "BindingRingShadow:" + i, new Vector2(-248, y - 2), new Vector2(38, 8), Edge);
            Box(p, "BindingRing:" + i, new Vector2(-249, y + 2), new Vector2(37, 5), Worn);
            Box(p, "BindingRingShine:" + i, new Vector2(-249, y + 4), new Vector2(32, 1), new Color(.8f, .8f, .71f, 1));
            Box(p, "BindingRingReturn:" + i, new Vector2(-264, y - 2), new Vector2(3, 10), Edge);
        }
        // Small chips along the outer edge keep the sheet from looking like a flat card.
        for (int i = 0; i < 14; i++)
        {
            float x = -224 + i * 33;
            Box(p, "PaperWearBottom:" + i, new Vector2(x, -323 + i % 3), new Vector2(13 + i % 4 * 4, 3), Worn);
        }
        Line(p, "HeaderRuleLeft", new Vector2(-219, 282), new Vector2(-129, 282), 2, Blue);
        Line(p, "HeaderRuleLeftEnd", new Vector2(-129, 282), new Vector2(-124, 275), 2, Blue);
        Line(p, "HeaderRuleRight", new Vector2(129, 282), new Vector2(213, 282), 2, Blue);
        Line(p, "HeaderRuleRightEnd", new Vector2(213, 282), new Vector2(219, 289), 2, Blue);
        var stamp = new Color(Blue.r, Blue.g, Blue.b, .13f);
        var center = new Vector2(161, 249);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
            Line(p, "LabStamp:" + i,
                center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 34,
                center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 34, 2, stamp);
        }
        Outline(p, "LabStampCore", center, new Vector2(20, 20), 2, stamp);
        for (int i = -1; i <= 1; i++)
        {
            Box(p, "LabStampPinTop:" + i, center + new Vector2(i * 7, 16), new Vector2(2, 7), stamp);
            Box(p, "LabStampPinBottom:" + i, center + new Vector2(i * 7, -16), new Vector2(2, 7), stamp);
        }
    }

    internal static bool DrawItem(Transform parent, string id, Vector2 at, Vector2 bounds)
    {
        var sprite = ResolveSprite(id);
        if (sprite == null) return false;
        var image = Box(parent, "ForgeGuideItem:" + id, at, bounds, Color.white);
        image.sprite = sprite;
        image.preserveAspect = true;
        return true;
    }

    private static Sprite? ResolveSprite(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (Sprites.TryGetValue(id, out var cached) && cached != null) return cached;
        Sprite? sprite;
        // Borrow the real item's sprite without retaining the temporary item.
        GameItem? prototype = null;
        try
        {
            prototype = DirectoryMaster.Item(id, false);
            sprite = prototype == null ? null : RenderHandler.LoadFromAtlas(prototype.spriteAtlasPath, prototype.spritePath);
        }
        finally
        {
            if (prototype != null && prototype.parentInventory == null) prototype.Destroy();
        }
        if (sprite == null || sprite.Pointer == IntPtr.Zero || sprite == RenderHandler.unknownSprite) return null;
        Sprites[id] = sprite;
        return sprite;
    }

    internal static void ClearScene() => Sprites.Clear();
}
