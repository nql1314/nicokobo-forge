using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace Nicokobo.Forge;

// Keep the game's EventSystem running: native tooltip raycasts require it.
// A transparent top Canvas wins UI raycasts while the IMGUI workshop is modal.
internal static class ForgeWorkshopInputShield
{
    private static GameObject? _root;
    private static bool _creationFailed;

    internal static void Show(Action<string> log)
    {
        if (_creationFailed) return;
        GameObject? created = null;
        try
        {
            if (_root == null || _root.Pointer == IntPtr.Zero)
            {
                var types = new Il2CppReferenceArray<Il2CppSystem.Type>(4);
                types[0] = Il2CppType.Of<RectTransform>();
                types[1] = Il2CppType.Of<Canvas>();
                types[2] = Il2CppType.Of<GraphicRaycaster>();
                types[3] = Il2CppType.Of<Image>();
                var root = new GameObject("Nico Workshop Input Shield", types);
                created = root;
                var canvas = root.GetComponent<Canvas>()
                    ?? throw new InvalidOperationException("Input shield Canvas missing");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var image = root.GetComponent<Image>()
                    ?? throw new InvalidOperationException("Input shield Image missing");
                image.color = new Color(0f, 0f, 0f, 0.001f);
                image.raycastTarget = true;
                UnityEngine.Object.DontDestroyOnLoad(root);
                _root = root;
                log("[NicokoboForge/Workshop] transparent UI input shield created");
            }
            _root.SetActive(true);
        }
        catch (Exception ex)
        {
            _creationFailed = true;
            try { created?.SetActive(false); } catch { }
            Hide();
            log($"[ERROR] [NicokoboForge/Workshop] input shield unavailable; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    internal static void Hide()
    {
        try { _root?.SetActive(false); }
        catch { /* The scene may have destroyed the shield. */ }
    }
}
