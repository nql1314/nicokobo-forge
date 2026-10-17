using System.Reflection;

namespace Nicokobo.Forge.Runtime
{
    // Synthetic resource/cache port. No Unity or IL2CPP calls in this executable.
    internal sealed class NativeSpriteAtlas
    {
        internal readonly Dictionary<string, UnityEngine.Sprite> Atlas = new();
    }
    internal static class NativeSpriteAtlasPort
    {
        internal static ISpriteAtlasPort<NativeSpriteAtlas>? Next;
        internal static ISpriteAtlasPort<NativeSpriteAtlas>? TryCreate(string key, Assembly assembly, ForgeSpriteFilter filter) => Next;
    }
}

namespace Il2Cpp
{
    public sealed class SpriteDict
    {
        public static SpriteDict? Instance;
        public Dictionary<string, UnityEngine.Sprite>? spriteDictionary = new();
        public bool FailReadback;
        public UnityEngine.Sprite? GetSprite(string key) => FailReadback ? null : spriteDictionary!.GetValueOrDefault(key);
    }
    public sealed partial class GameItem
    {
        public IntPtr Pointer { get; set; } = new(1);
        public string? spriteAtlasPath, spritePath;
        public int SpriteAssignments;
        public GameItem SetSprite(string atlas, string sprite)
        { SpriteAssignments++; spriteAtlasPath = atlas; spritePath = sprite; return this; }
    }
}

namespace UnityEngine
{
    public sealed class Sprite : Object
    {
        private static int _next;
        public IntPtr Pointer { get; } = new(++_next);
        public string? name;
        public HideFlags hideFlags;
        public Texture2D? texture;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
            => new() { texture = texture };
    }
}
