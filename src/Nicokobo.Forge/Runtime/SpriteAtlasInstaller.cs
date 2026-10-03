namespace Nicokobo.Forge.Runtime;

// Separate publication/compensation from Unity so failures after a native write
// can be checked without constructing textures or starting the game.
internal interface ISpriteAtlasPort<TAtlas> where TAtlas : class
{
    bool Occupied { get; }
    TAtlas Create();
    void Load(TAtlas atlas, ForgeSpriteDefinition definition);
    void Publish(TAtlas atlas);
    bool IsCurrent(TAtlas atlas);
    void Verify(TAtlas atlas, ForgeSpriteDefinition definition);
    void Remove();
    void Release(TAtlas atlas);
}

internal sealed record SpriteAtlasInstallResult<TAtlas>(bool Installed, TAtlas? Retained,
    Exception? Error) where TAtlas : class;

internal static class SpriteAtlasInstaller
{
    internal static SpriteAtlasInstallResult<TAtlas> Install<TAtlas>(
        IReadOnlyList<ForgeSpriteDefinition> definitions, ISpriteAtlasPort<TAtlas> port)
        where TAtlas : class
    {
        TAtlas? atlas = null;
        bool attemptedPublish = false;
        try
        {
            if (port.Occupied) throw new InvalidOperationException("Atlas key is already in use");
            atlas = port.Create();
            foreach (var definition in definitions) port.Load(atlas, definition);
            if (port.Occupied) throw new InvalidOperationException("Atlas key changed during loading");
            attemptedPublish = true;
            port.Publish(atlas);
            foreach (var definition in definitions) port.Verify(atlas, definition);
            if (!port.IsCurrent(atlas)) throw new InvalidOperationException("Atlas publication changed during readback");
            return new(true, atlas, null);
        }
        catch (Exception error)
        {
            if (atlas == null) return new(false, null, error);
            try
            {
                if (attemptedPublish && port.IsCurrent(atlas))
                {
                    port.Remove();
                    if (port.IsCurrent(atlas)) throw new InvalidOperationException("Atlas removal did not take effect");
                }
                port.Release(atlas);
                return new(false, null, error);
            }
            catch (Exception restore)
            {
                // Keep resources alive when cache removal or cleanup cannot be
                // verified. A live cache must never reference destroyed sprites.
                return new(false, atlas, new AggregateException(error, restore));
            }
        }
    }
}
