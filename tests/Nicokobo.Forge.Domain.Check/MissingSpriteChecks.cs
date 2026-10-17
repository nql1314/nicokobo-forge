using Nicokobo.Forge.Runtime;
using UnityEngine;

internal static class MissingSpriteChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception(message); }
        Sprite? Read(Type type, string method, Sprite? result)
        {
            object[] arguments = [result!];
            NativeHookSet.Postfix(type, method, arguments);
            return (Sprite?)arguments[0];
        }
        Action<string> log = _ => { };
        NativeHookSet.Installed = [];
        MissingSpriteRuntime.Install(true, log);
        var placeholder = Read(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas", null);
        Check(placeholder != null, "Missing atlas and unavailable native dummy returned a null sprite");
        Check(placeholder!.texture is { width: 32, height: 32, Applied: true },
            "Emergency sprite has no usable texture");
        Check(placeholder.texture!.Pixels!.Distinct().Count() == 2, "Emergency icon is not visible");
        foreach (var (type, method) in new[] {
            (typeof(Il2Cpp.RenderHandler), "LoadFromAtlas"),
            (typeof(Il2Cpp.RenderHandler), "LoadFromFile"),
            (typeof(Il2Cpp.IconProvider), "GetIcon") })
        {
            var live = new Sprite();
            Check(ReferenceEquals(Read(type, method, live), live), "Valid native/mod icon was replaced");
            Check(ReferenceEquals(Read(type, method, null), placeholder), "Missing icon did not reuse placeholder");
            Check(ReferenceEquals(Read(type, method, new Sprite { Destroyed = true }), placeholder),
                "Destroyed Unity wrapper escaped the guard");
        }
        var native = new Sprite();
        Il2Cpp.RenderHandler.unknownSprite = native;
        Check(ReferenceEquals(Read(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas", null), native),
            "Available native unknown icon was not preferred");
        native.Destroyed = true;
        Check(ReferenceEquals(Read(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas", null), placeholder),
            "Destroyed native unknown icon was returned");
        placeholder.Destroyed = true;
        var recreated = Read(typeof(Il2Cpp.IconProvider), "GetIcon", null);
        Check(recreated != null && !ReferenceEquals(recreated, placeholder), "Destroyed placeholder was reused");
        recreated!.texture!.Destroyed = true;
        var newTexture = Read(typeof(Il2Cpp.IconProvider), "GetIcon", null);
        Check(newTexture != null && newTexture.texture != null &&
            !ReferenceEquals(newTexture.texture, recreated.texture), "Destroyed placeholder texture was reused");
        Check(ReferenceEquals(Il2Cpp.RenderHandler.unknownSprite, native), "Native dummy field was overwritten");
        MissingSpriteRuntime.Uninstall();
        Check(recreated != null, "Uninstall destroyed an icon still held by UI");
        Check(NativeHookSet.Installed.Count == 0, "Uninstall left hooks installed");
        MissingSpriteRuntime.Install(false, log);
        Check(NativeHookSet.Installed.Count == 0, "Unknown build enabled image hooks");
        NativeHookSet.InstallResult = false;
        MissingSpriteRuntime.Install(true, log);
        Check(NativeHookSet.Installed.Count == 0, "Failed hook installation left active image hooks");
        NativeHookSet.InstallResult = true;
        MissingSpriteRuntime.Install(true, log);
        Check(NativeHookSet.Installed.Count == 3, "Retry after failed installation did not install the whole set");
        MissingSpriteRuntime.Uninstall();
        Il2Cpp.RenderHandler.unknownSprite = null;
        Console.WriteLine($"Missing sprite checks passed: {checks}.");
    }
}
