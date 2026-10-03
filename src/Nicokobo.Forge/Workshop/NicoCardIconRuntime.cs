using Il2Cpp;
using Nicokobo.Forge;

namespace Nicokobo.Forge.Workshop;

internal static class NicoCardIconRuntime
{
    internal const string AtlasKey = "Nicokobo/Forge/NicoWorkshop";
    internal const string SpriteKey = "nicokobo_forge_nico_card";
    internal const int SpriteWidth = 32;
    internal const int SpriteHeight = 16;
    private const string ResourceName =
        "Nicokobo.Forge.Assets.Icons.nico_card.png";

    private static readonly ForgeSpriteAtlas Atlas = ForgeAssetsApi.CreateEmbeddedSpriteAtlas(
        "nicokobo.forge", AtlasKey, typeof(NicoCardIconRuntime).Assembly,
        [new(SpriteKey, ResourceName, SpriteWidth, SpriteHeight)], ForgeSpriteFilter.Bilinear);
    private static Action<string>? _log;
    private static Action<string>? _warn;
    private static readonly Action OnInstalled = () => _log?.Invoke(
        $"[NicokoboForge/Workshop] Icon installed; atlas={AtlasKey}; sprite={SpriteKey}; display={SpriteWidth}x{SpriteHeight}; filter=Bilinear");
    private static readonly Action<Exception> OnFailure = ex => _warn?.Invoke(
        $"[NicokoboForge/Workshop] Icon unavailable; {ex.GetType().Name}: {ex.Message}");

    internal static void Configure(Action<string> log, Action<string> warn)
    {
        _log = log;
        _warn = warn;
    }

    internal static bool TryInstall() => Atlas.TryInstall(OnInstalled, OnFailure);

    internal static void Assign(GameItem item)
    {
        TryInstall();
        Atlas.Assign(item, SpriteKey);
    }

    internal static bool MatchesShape(GameItem item) => item.shape != null &&
        item.shape.Pointer != IntPtr.Zero && item.shape.width == 2 &&
        item.shape.height == 1 && item.shape.globalWidth == 2 &&
        item.shape.globalHeight == 1;
}
