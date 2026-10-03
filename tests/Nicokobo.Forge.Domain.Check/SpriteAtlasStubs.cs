using System.Reflection;

namespace Nicokobo.Forge.Runtime
{
    // Synthetic resource/cache port. No Unity or IL2CPP calls in this executable.
    internal sealed class NativeSpriteAtlas { }
    internal static class NativeSpriteAtlasPort
    {
        internal static ISpriteAtlasPort<NativeSpriteAtlas>? Next;
        internal static ISpriteAtlasPort<NativeSpriteAtlas>? TryCreate(string key, Assembly assembly, ForgeSpriteFilter filter) => Next;
    }
}

namespace Il2Cpp
{
    public sealed partial class GameItem
    {
        public IntPtr Pointer { get; set; } = new(1);
        public string? spriteAtlasPath, spritePath;
        public int SpriteAssignments;
        public GameItem SetSprite(string atlas, string sprite)
        { SpriteAssignments++; spriteAtlasPath = atlas; spritePath = sprite; return this; }
    }
}
