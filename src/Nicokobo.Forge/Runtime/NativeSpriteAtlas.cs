using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using AtlasDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, UnityEngine.Sprite>;
using CacheDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.Dictionary<string, UnityEngine.Sprite>>;

namespace Nicokobo.Forge.Runtime;

internal sealed class NativeSpriteAtlas
{
    internal readonly AtlasDictionary Atlas = new();
    internal readonly List<Texture2D> Textures = [];
    internal readonly List<Sprite> Sprites = [];
}

internal sealed class NativeSpriteAtlasPort(string key, Assembly assembly,
    ForgeSpriteFilter filter, CacheDictionary cache) : ISpriteAtlasPort<NativeSpriteAtlas>
{
    internal static NativeSpriteAtlasPort? TryCreate(string key, Assembly assembly, ForgeSpriteFilter filter)
    {
        var cache = RenderHandler.atlasCache;
        return cache == null || cache.Pointer == IntPtr.Zero ? null : new(key, assembly, filter, cache);
    }

    public bool Occupied => cache.ContainsKey(key);
    public NativeSpriteAtlas Create() => new();
    public void Publish(NativeSpriteAtlas atlas) => cache.Add(key, atlas.Atlas);
    public bool IsCurrent(NativeSpriteAtlas atlas) => cache.ContainsKey(key) &&
        cache[key]?.Pointer == atlas.Atlas.Pointer;
    public void Remove() => cache.Remove(key);

    public void Load(NativeSpriteAtlas atlas, ForgeSpriteDefinition definition)
    {
        using var stream = assembly.GetManifestResourceStream(definition.ResourceName)
            ?? throw new InvalidOperationException("Embedded icon missing: " + definition.ResourceName);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (bytes.Length == 0) throw new InvalidOperationException("Embedded icon is empty: " + definition.ResourceName);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        atlas.Textures.Add(texture);
        texture.name = definition.Key + "_texture";
        texture.filterMode = filter == ForgeSpriteFilter.Point ? FilterMode.Point : FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;
        if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(bytes), false) ||
            texture.width != definition.Width || texture.height != definition.Height)
            throw new InvalidOperationException($"Icon decode or size contract failed: {definition.Key}; " +
                $"expected={definition.Width}x{definition.Height}; actual={texture.width}x{texture.height}");
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 1), 100);
        if (sprite == null || sprite.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Sprite creation failed: " + definition.Key);
        atlas.Sprites.Add(sprite);
        sprite.name = definition.Key;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        atlas.Atlas.Add(definition.Key, sprite);
    }

    public void Verify(NativeSpriteAtlas atlas, ForgeSpriteDefinition definition)
    {
        var readback = RenderHandler.LoadFromAtlas(key, definition.Key);
        if (readback == null || readback.Pointer == IntPtr.Zero ||
            readback.Pointer != atlas.Atlas[definition.Key].Pointer)
            throw new InvalidOperationException("Atlas readback failed: " + definition.Key);
    }

    public void Release(NativeSpriteAtlas atlas)
    {
        var errors = new List<Exception>();
        foreach (var resource in atlas.Sprites.Cast<UnityEngine.Object>().Concat(atlas.Textures))
            try { UnityEngine.Object.Destroy(resource); }
            catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0) throw new AggregateException(errors);
        atlas.Sprites.Clear(); atlas.Textures.Clear();
    }
}
