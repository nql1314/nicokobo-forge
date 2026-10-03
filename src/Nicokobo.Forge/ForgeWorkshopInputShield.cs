using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nicokobo.Forge.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Nicokobo.Forge;

// Keep the game's EventSystem running: native tooltip raycasts require it.
// Only the workshop rectangle and gestures it owns block native input.
internal static class ForgeWorkshopInputShield
{
    private static GameObject? _root;
    private static RectTransform? _hitArea;
    private static bool _creationFailed;
    private static bool _installed;

    internal static bool Install(Action<string> log)
    {
        if (_installed) return true;
        _installed = NativeHookSet.Install("nicokobo.forge.workshop.input",
        [
            new(typeof(InputActionManager), nameof(InputActionManager.Update), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeGameInput)),
            new(typeof(CustomEventHandler), nameof(CustomEventHandler.Update), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeDoubleClick)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.CheckRaycast), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeStoreRaycast)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.Update), [], typeof(void),
                typeof(ForgeWorkshopInputShield), Postfix: nameof(AfterStoreUpdate)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape), [], typeof(bool),
                typeof(ForgeWorkshopInputShield), Prefix: nameof(BeforeEscape))
        ], log);
        if (_installed) log("[NicokoboForge/Workshop] window-scoped native input guards installed");
        return _installed;
    }

    private static bool BeforeGameInput(InputActionManager __instance)
    {
        if (PointerOverWindow && (Input.GetMouseButtonUp(0) ||
            Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)))
        {
            // Native drag targeting skips plain overlay Images. Cancel before
            // release dispatch so an outside drag cannot drop behind this window.
            var drag = ItemMouseDragHandler.current;
            if (drag != null && drag.currentItem != null) drag.OnEventReset();
        }
        if (!ForgeWorkshopApi.BlocksNativeInput) return true;
        __instance.Reset();
        __instance.lastMousePosition = Input.mousePosition;
        return false;
    }

    private static bool BeforeDoubleClick(CustomEventHandler __instance)
    {
        if (!ForgeWorkshopApi.BlocksNativeInput) return true;
        __instance.lastHandlerClicked = null;
        __instance.hasRayCast = false;
        __instance.timeDeltaSeconds = __instance.doubleClickTimeThreshold;
        return false;
    }

    private static bool PointerOverWindow => ForgeWorkshopApi.IsVisible &&
        ForgeWorkshopOverlay.ContainsPointer(Input.mousePosition);

    private static bool BeforeStoreRaycast(StoreUIManager __instance)
    {
        if (!PointerOverWindow && !ForgeWorkshopApi.BlocksNativeInput) return true;
        // CheckRaycast only queries the game's canvases, ignoring our overlay.
        __instance.hoveringStoreUI = true;
        return false;
    }

    private static void AfterStoreUpdate(StoreUIManager __instance)
    {
        if (PointerOverWindow || ForgeWorkshopApi.BlocksNativeInput)
            __instance.hoveringStoreUI = true;
    }

    internal static void ResetOpeningDrag()
    {
        // Double-click activation can leave a pending inventory press. Use the
        // native reset to finish the visual drag and clear its press target.
        var drag = ItemMouseDragHandler.current;
        if (drag != null && drag.currentItem != null) drag.OnEventReset();
        InputActionManager.current?.Reset();
        CustomEventHandler.ClearDoubleClickTarget();
    }

    private static bool BeforeEscape(ref bool __result)
    {
        if (!ForgeWorkshopApi.IsVisible && !ForgeWorkshopApi.BlocksNativeInput) return true;
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
                var hitArea = new GameObject("Workshop Window Hit Area", hitTypes);
                hitArea.transform.SetParent(root.transform, false);
                var rect = hitArea.GetComponent<RectTransform>()
                    ?? throw new InvalidOperationException("Input shield RectTransform missing");
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                _hitArea = rect;
                var image = hitArea.GetComponent<Image>()
                    ?? throw new InvalidOperationException("Input shield Image missing");
                image.color = new Color(0f, 0f, 0f, 0.001f);
                image.raycastTarget = true;
                UnityEngine.Object.DontDestroyOnLoad(root);
                _root = root;
                log("[NicokoboForge/Workshop] window UI input shield created");
            }
            var bounds = ForgeWorkshopOverlay.ScreenRect();
            _hitArea!.anchoredPosition = new Vector2(bounds.x, bounds.y);
            _hitArea.sizeDelta = new Vector2(bounds.width, bounds.height);
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
            _hitArea = null;
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
