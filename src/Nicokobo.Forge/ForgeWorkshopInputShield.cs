using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nicokobo.Forge.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Nicokobo.Forge;

// Keep the game's EventSystem running: native tooltip raycasts require it.
// Block native input polling as well as UI raycasts while the workshop is modal.
internal static class ForgeWorkshopInputShield
{
    private static GameObject? _root;
    private static bool _creationFailed;
    private static bool _installed;

    internal static bool Install(Action<string> log)
    {
        if (_installed) return true;
        _installed = NativeHookSet.Install("nicokobo.forge.workshop.input",
        [
            new(typeof(InputActionManager), nameof(InputActionManager.Update), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeGameInput)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.CheckRaycast), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeStoreRaycast)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape), [], typeof(bool),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeEscape))
        ], log);
        if (_installed) log("[NicokoboForge/Workshop] native modal input guards installed");
        return _installed;
    }

    private static bool BeforeGameInput() => !ForgeWorkshopApi.BlocksNativeInput;

    private static bool BeforeStoreRaycast(StoreUIManager __instance)
    {
        if (!ForgeWorkshopApi.BlocksNativeInput) return true;
        // CheckRaycast only queries the game's canvases, ignoring our overlay.
        __instance.hoveringStoreUI = true;
        return false;
    }

    private static bool BeforeEscape(ref bool __result)
    {
        if (!ForgeWorkshopApi.BlocksNativeInput) return true;
        ForgeWorkshopApi.CloseWindow();
        __result = true;
        return false;
    }

    internal static bool Show(Action<string> log)
    {
        if (_creationFailed || !_installed) return false;
        GameObject? created = null;
        try
        {
            if (_root == null || _root.Pointer == IntPtr.Zero)
            {
                var types = new Il2CppReferenceArray<Il2CppSystem.Type>(3);
                types[0] = Il2CppType.Of<RectTransform>();
                types[1] = Il2CppType.Of<Canvas>();
                types[2] = Il2CppType.Of<GraphicRaycaster>();
                var root = new GameObject("Nico Workshop Input Shield", types);
                created = root;
                root.SetActive(false);
                var canvas = root.GetComponent<Canvas>()
                    ?? throw new InvalidOperationException("Input shield Canvas missing");
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var hitTypes = new Il2CppReferenceArray<Il2CppSystem.Type>(3);
                hitTypes[0] = Il2CppType.Of<RectTransform>();
                hitTypes[1] = Il2CppType.Of<CanvasRenderer>();
                hitTypes[2] = Il2CppType.Of<Image>();
                var hitArea = new GameObject("Full Screen Hit Area", hitTypes);
                hitArea.transform.SetParent(root.transform, false);
                var rect = hitArea.GetComponent<RectTransform>()
                    ?? throw new InvalidOperationException("Input shield RectTransform missing");
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = hitArea.GetComponent<Image>()
                    ?? throw new InvalidOperationException("Input shield Image missing");
                image.color = new Color(0f, 0f, 0f, 0.001f);
                image.raycastTarget = true;
                UnityEngine.Object.DontDestroyOnLoad(root);
                _root = root;
                log("[NicokoboForge/Workshop] transparent UI input shield created");
            }
            _root.SetActive(true);
            return true;
        }
        catch (Exception ex)
        {
            _creationFailed = true;
            try { created?.SetActive(false); } catch { }
            Hide();
            try { if (created != null) UnityEngine.Object.Destroy(created); } catch { }
            _root = null;
            log($"[ERROR] [NicokoboForge/Workshop] input shield unavailable; " +
                $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static void Hide()
    {
        try { _root?.SetActive(false); }
        catch { /* The scene may have destroyed the shield. */ }
    }
}
