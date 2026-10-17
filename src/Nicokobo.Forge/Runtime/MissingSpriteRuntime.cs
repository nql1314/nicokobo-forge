using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace Nicokobo.Forge.Runtime;

// Guard rendering results, not saved paths or the raw atlas lookup contract.
// Missing mod artwork can become available later in the same process.
internal static class MissingSpriteRuntime
{
    private const string HookId = "nicokobo.forge.missing_sprites";
    private static bool _installed;
    private static Sprite? _placeholder;
    private static Texture2D? _texture;

    internal static void Install(bool knownBuild, Action<string> log)
    {
        if (_installed || !knownBuild) return;
        try
        {
            NativeHookSet.Require(typeof(RenderHandler), "get_unknownSprite", [], typeof(Sprite));
            _installed = NativeHookSet.Install(HookId,
            [
                new(typeof(RenderHandler), nameof(RenderHandler.LoadFromAtlas),
                    [typeof(string), typeof(string)], typeof(Sprite), typeof(MissingSpriteRuntime),
                    Postfix: nameof(AfterRead)),
                new(typeof(RenderHandler), nameof(RenderHandler.LoadFromFile),
                    [typeof(string)], typeof(Sprite), typeof(MissingSpriteRuntime), Postfix: nameof(AfterRead)),
                new(typeof(IconProvider), nameof(IconProvider.GetIcon),
                    [typeof(NewGameData.StartType)], typeof(Sprite), typeof(MissingSpriteRuntime),
                    Postfix: nameof(AfterRead))
            ], log);
            if (_installed) log("[INFO] [NicokoboForge/Assets] missing sprite guards installed");
        }
        catch (Exception ex)
        {
            log($"[WARN] [NicokoboForge/Assets] missing sprite guards disabled: {ex.Message}");
        }
    }

    internal static void Uninstall()
    {
        if (!_installed) return;
        NativeHookSet.Remove(HookId, null);
        _installed = false;
        // UI may still reference these resources. Retain them for the process lifetime.
    }

    [HarmonyPriority(Priority.Last)]
    private static void AfterRead(ref Sprite __result)
    {
        // Unity equality detects destroyed native objects as well as managed nulls.
        if (__result != null) return;
        var native = RenderHandler.unknownSprite;
        __result = native != null ? native : Placeholder();
    }

    internal static Sprite Placeholder()
    {
        if (_placeholder != null && _texture != null) return _placeholder;
        if (_texture == null)
        {
            const int side = 32;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false, false)
            {
                name = "nicokobo.forge.missing_sprite_texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var dark = new Color(0.15f, 0.15f, 0.18f, 1f);
            var bright = new Color(0.85f, 0.3f, 0.75f, 1f);
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    texture.SetPixel(x, y, ((x / 8 + y / 8) & 1) == 0 ? dark : bright);
            // Keep readable for native sprite/shape consumers.
            texture.Apply(false, false);
            _texture = texture;
        }
        _placeholder = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height),
            new Vector2(0.5f, 0.5f), 100);
        if (_placeholder == null) throw new InvalidOperationException("Missing sprite placeholder creation failed");
        _placeholder.name = "nicokobo.forge.missing_sprite";
        _placeholder.hideFlags = HideFlags.HideAndDontSave;
        return _placeholder;
    }
}
