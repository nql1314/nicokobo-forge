namespace Il2Cpp
{
    public static class RenderHandler
    {
        public static UnityEngine.Sprite? unknownSprite;
        public static UnityEngine.Sprite? LoadFromAtlas(string atlas, string name) => null;
        public static UnityEngine.Sprite? LoadFromFile(string path) => null;
    }
    public sealed class IconProvider { public UnityEngine.Sprite? GetIcon(NewGameData.StartType start) => null; }
    public sealed class NewGameData { public enum StartType { Apprentice = 1 } }
}

namespace HarmonyLib
{
    public sealed class HarmonyPriority(int priority) : System.Attribute { public int Value = priority; }
    public static class Priority { public const int First = 800, Last = 0; }
}

namespace UnityEngine
{
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Point }
    public enum TextureWrapMode { Clamp }
    public enum HideFlags { HideAndDontSave }
    public readonly record struct Color(float R, float G, float B, float A);
    public readonly record struct Rect(float X, float Y, float Width, float Height);
    public readonly record struct Vector2(float X, float Y);
    public sealed class Texture2D(int w, int h, TextureFormat format, bool mipChain, bool linear) : Object
    {
        public TextureFormat Format = format;
        public bool MipChain = mipChain, Linear = linear;
        public int width = w, height = h;
        public string? name;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public HideFlags hideFlags;
        public Color[]? Pixels;
        public bool Applied;
        public void SetPixel(int x, int y, Color color)
        { Pixels ??= new Color[width * height]; Pixels[y * width + x] = color; }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) => Applied = true;
    }
}
