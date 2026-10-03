using System.Reflection;
using System.Text.RegularExpressions;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Process-lifetime atlas ownership. Content mods keep their own
/// artwork, atlas paths and sprite keys. Call installation/assignment on the game thread.</summary>
public static class ForgeAssetsApi
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ForgeSpriteAtlas> Atlases = new(StringComparer.Ordinal);
    private static readonly Regex OwnerPattern = new("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$",
        RegexOptions.CultureInvariant);

    public static ForgeSpriteAtlas CreateEmbeddedSpriteAtlas(string ownerId, string atlasKey,
        Assembly assembly, IReadOnlyList<ForgeSpriteDefinition> definitions,
        ForgeSpriteFilter filter = ForgeSpriteFilter.Point)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !OwnerPattern.IsMatch(ownerId) ||
            string.IsNullOrWhiteSpace(atlasKey) || assembly == null || !Enum.IsDefined(filter) ||
            definitions is not { Count: > 0 })
            throw new ArgumentException("An owner, atlas key, resource assembly and sprite definitions are required");
        var frozen = definitions.ToArray();
        if (frozen.Any(d => d == null || string.IsNullOrWhiteSpace(d.Key) ||
                string.IsNullOrWhiteSpace(d.ResourceName) || d.Width <= 0 || d.Height <= 0) ||
            frozen.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count() != frozen.Length)
            throw new ArgumentException("Sprite keys must be unique and dimensions must be positive");
        lock (Gate)
        {
            if (Atlases.TryGetValue(atlasKey, out var existing))
            {
                if (existing.Matches(ownerId, assembly, frozen, filter)) return existing;
                throw new InvalidOperationException($"Atlas key already claimed by {existing.OwnerId}: {atlasKey}");
            }
            var atlas = new ForgeSpriteAtlas(ownerId, atlasKey, assembly, frozen, filter);
            Atlases.Add(atlasKey, atlas);
            return atlas;
        }
    }
}

public sealed class ForgeSpriteAtlas
{
    private readonly Assembly _assembly;
    private readonly ForgeSpriteDefinition[] _definitions;
    private readonly HashSet<string> _keys;
    private readonly ForgeSpriteFilter _filter;
    private NativeSpriteAtlas? _resources;
    private long _nextAttempt;
    private bool _attempted, _installing;

    internal ForgeSpriteAtlas(string ownerId, string atlasKey, Assembly assembly,
        ForgeSpriteDefinition[] definitions, ForgeSpriteFilter filter)
    {
        OwnerId = ownerId; AtlasKey = atlasKey; _assembly = assembly;
        _definitions = definitions; _filter = filter;
        _keys = definitions.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
    }

    public string OwnerId { get; }
    public string AtlasKey { get; }
    public bool Installed { get; private set; }
    public bool Failed { get; private set; }
    public int Count => Installed ? _definitions.Length : 0;

    internal bool Matches(string ownerId, Assembly assembly, ForgeSpriteDefinition[] definitions,
        ForgeSpriteFilter filter) => OwnerId == ownerId && _assembly == assembly &&
        _filter == filter && _definitions.SequenceEqual(definitions);

    /// <summary>False while the native cache is not ready; retry is throttled.
    /// A conflict or failed publication is terminal. Notifications fire once
    /// per attempted installation; throwing notifications cannot change the result.</summary>
    public bool TryInstall(Action? onInstalled = null, Action<Exception>? onFailure = null)
    {
        if (Installed) return true;
        if (Failed || _installing) return false;
        long now = Environment.TickCount64;
        if (_attempted && now < _nextAttempt) return false;
        _attempted = true;
        _nextAttempt = now + ForgeNumbers.Assets.RetryMilliseconds;
        _installing = true;
        Exception? error = null;
        try
        {
            var port = NativeSpriteAtlasPort.TryCreate(AtlasKey, _assembly, _filter);
            if (port == null) return false;
            var result = SpriteAtlasInstaller.Install(_definitions, port);
            _resources = result.Retained;
            Installed = result.Installed;
            Failed = !Installed;
            error = result.Error;
        }
        catch (Exception ex) { Failed = true; error = ex; }
        finally { _installing = false; }
        try
        {
            if (Installed) onInstalled?.Invoke();
            else if (error != null) onFailure?.Invoke(error);
        }
        catch { /* Diagnostics must not invalidate a published atlas. */ }
        return Installed;
    }

    /// <summary>Assign an installed sprite, or optionally stage its paths while
    /// awaiting the cache. Staged paths do not prove a sprite was rendered.</summary>
    public void Assign(GameItem item, string spriteKey, bool allowStaging = false)
    {
        if (item == null || item.Pointer == IntPtr.Zero || !_keys.Contains(spriteKey))
            throw new ArgumentException("A live item and a declared sprite key are required");
        if (Failed) throw new InvalidOperationException("Sprite atlas installation failed: " + AtlasKey);
        if (Installed)
        {
            var assigned = item.SetSprite(AtlasKey, spriteKey);
            if (assigned == null || assigned.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native sprite assignment failed: " + spriteKey);
        }
        else if (allowStaging) { item.spriteAtlasPath = AtlasKey; item.spritePath = spriteKey; }
        else throw new InvalidOperationException("Sprite atlas is not ready: " + AtlasKey);
        if (item.spriteAtlasPath != AtlasKey || item.spritePath != spriteKey)
            throw new InvalidOperationException("Sprite assignment paths do not match: " + spriteKey);
        GC.KeepAlive(_resources);
    }
}
