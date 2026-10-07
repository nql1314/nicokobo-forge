using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using Nicokobo.Forge.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nicokobo.Forge;

internal static partial class RecipeGuideBrowser
{
    private const string HookId = "nicokobo.forge.recipe_guide";
    private sealed record HoverTarget(RectTransform Area, string Title, string Description);
    private static readonly List<HoverTarget> HoverTargets = new();
    private static readonly ForgeWindowInputCapture Capture = new();
    private static IReadOnlyList<RecipeGuidePage> _pages = Array.Empty<RecipeGuidePage>();
    private static GameObject? _root, _bookRoot, _hoverRoot;
    private static Transform? _pageHost;
    private static RectTransform? _paper;
    private static TMP_FontAsset? _font;
    private static TextMeshProUGUI? _title, _issuer, _pageNumber, _hoverTitle, _hoverDescription;
    private static Image? _hoverPaper;
    private static HoverTarget? _hoverTarget;
    private static Button? _left, _right;
    private static StoreUIManager? _lockedStore;
    private static string? _currentTargetId;
    private static bool _english, _installed;
    private static int _page, _lastBrowsePage, _screenWidth, _screenHeight;
    private static Action<string>? _log;
    private static readonly Color Paper = new(.77f, .77f, .68f, 1);
    private static readonly Color Ink = new(.24f, .23f, .28f, 1);
    private static readonly Color Blue = new(.29f, .40f, .48f, 1);
    private static readonly Color Red = new(.58f, .28f, .27f, 1);
    private static readonly Color Muted = new(.53f, .53f, .47f, 1);
    internal static bool Visible => _root != null && _root.activeSelf;
    private static bool PointerOverWindow => Visible && RecipeGuideWindowDrag.ContainsPointer(_paper?.gameObject);
    private static bool MouseHeld => Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);

    internal static void Install(bool knownBuild, Action<string> log)
    {
        _log = log;
        if (!knownBuild || _installed) return;
        var hooks = new List<NativeHook>
        {
            new(typeof(InputActionManager), nameof(InputActionManager.Update), [], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(InputPrefix)),
            new(typeof(CustomEventHandler), nameof(CustomEventHandler.Update), [], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(DoubleClickPrefix)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.CheckRaycast), [], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(RaycastPrefix)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.Update), [], typeof(void), typeof(RecipeGuideBrowser), Postfix: nameof(StoreUpdatePostfix)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.ResolveEscape), [], typeof(bool), typeof(RecipeGuideBrowser), Prefix: nameof(EscapePrefix)),
            new(typeof(StoreUIManager), nameof(StoreUIManager.CloseAllUI), [], typeof(void), typeof(RecipeGuideBrowser), Postfix: nameof(Close)),
            new(typeof(MultiPageUIManager), nameof(MultiPageUIManager.OpenUI), [typeof(int)], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(Close)),
            new(typeof(PaperUIManager), nameof(PaperUIManager.OpenBook), [typeof(GameItem)], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(Close))
        };
        foreach (var name in new[] { nameof(PaperUIManager.OpenCigaretteGuide), nameof(PaperUIManager.OpenStampGuide),
                     nameof(PaperUIManager.OpenInjectorGuide), nameof(PaperUIManager.OpenCigaretteColor),
                     nameof(PaperUIManager.OpenCodeBook), nameof(PaperUIManager.OpenAugIdGuide) })
            hooks.Add(new(typeof(PaperUIManager), name, [], typeof(void), typeof(RecipeGuideBrowser), Prefix: nameof(Close)));
        _installed = NativeHookSet.Install(HookId, hooks, log);
        if (_installed) log("[INFO] [NicokoboForge/Recipes] G item/ingredient/output queries and recipe window installed");
    }

    internal static void Update()
    {
        if (!_installed || !Application.isFocused || !Input.GetKeyDown(KeyCode.G)) return;
        try
        {
            if (ForgeTextInputFocus.ShouldSuppressHotkeys()) return;
            var store = StoreUIManager.Instance;
            if (PlayerStore.instance == null || EmporiumEntry.Instance?.invElement == null || store == null ||
                store.IsTyping() || ForgeWorkshopApi.IsVisible) return;
            var mouse = Input.mousePosition;
            var target = PointerOverWindow ? null : RenderHandler.RaycastElement<GameItemElement>(new Vector2(mouse.x, mouse.y));
            if (Visible && (target == null || target.identifier == _currentTargetId)) { Close(); return; }
            Open(target, store);
        }
        catch (Exception ex)
        {
            ClearScene();
            _log?.Invoke($"[ERROR] [NicokoboForge/Recipes] query unavailable: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Open(GameItem? target, StoreUIManager store)
    {
        Close();
        if (MultiPageUIManager.Instance?.panel.activeSelf == true) MultiPageUIManager.Instance.CloseUI();
        PaperUIManager.Instance?.CloseAllPaperUI();
        _english = ForgePresentationApi.PreferEnglish();
        _pages = RecipeGuideQuery.Build(ForgeRecipeGuideApi.Snapshot(), target?.identifier,
            target?.IsGameItemType("MACHINE") == true, _english, ResolveName, ForgeMachineAutomationApi.Recipes,
            tag => target?.IsTag(tag) == true);
        EnsureWindow();
        _font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(candidate =>
            candidate.name == (_english ? "NotoSans-Medium SDF" : "NotoSansSC-VariableFont_wght")) ?? _font;
        foreach (var label in new[] { _title, _issuer, _pageNumber,
                     _left!.GetComponentInChildren<TextMeshProUGUI>(), _right!.GetComponentInChildren<TextMeshProUGUI>() })
            label!.font = _font!;
        _issuer!.text = "NICOKOBO FORGE";
        _title!.text = target == null ? (_english ? "Recipe Browser" : "合成表")
            : ResolveName(target.identifier, _english) + (_english ? " · Recipes" : " · 相关配方");
        if (_bookRoot != null)
        {
            _bookRoot.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(_bookRoot);
        }
        HoverTargets.Clear();
        _bookRoot = Node("ForgeRecipeBook", _pageHost!);
        Position(_bookRoot.GetComponent<RectTransform>(), Vector2.zero, new Vector2(500, 550));
        for (int index = 0; index < _pages.Count; index++) BuildPage(_bookRoot.transform, _pages[index], index, HoverTargets);
        _currentTargetId = target?.identifier;
        _lockedStore = store;
        _root!.SetActive(true);
        Capture.Open(Time.frameCount, MouseHeld);
        var drag = ItemMouseDragHandler.current;
        if (drag != null && drag.currentItem != null) drag.OnEventReset();
        InputActionManager.current?.Reset();
        CustomEventHandler.ClearDoubleClickTarget();
        ShowPage(target == null ? _lastBrowsePage : 0);
        UpdateCursor();
    }

    private static string ResolveName(string id, bool english)
    {
        var registered = ForgePresentationApi.GetRegisteredItemName(id, english);
        if (!string.IsNullOrWhiteSpace(registered)) return registered;
        if (string.IsNullOrWhiteSpace(id)) return id;
        string nativeName = GeneralHelper.GetDisplayNameFromID(id);
        return string.IsNullOrWhiteSpace(nativeName) ? id : nativeName;
    }

    internal static void UpdateCursor()
    {
        if (!Visible) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_screenWidth != Screen.width || _screenHeight != Screen.height)
        {
            bool firstLayout = _screenWidth == 0;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            if (firstLayout)
            {
                float viewportWidth = _screenWidth * 720f / Math.Max(1, _screenHeight);
                _paper!.anchoredPosition = new Vector2(-Math.Max(0, Math.Min(300, (viewportWidth - 552) / 2)), 0);
            }
            else RecipeGuideWindowDrag.Clamp(_paper!);
        }
        UpdateHover();
    }

    internal static void Close()
    {
        HideHover();
        Capture.Close(Time.frameCount, MouseHeld);
        if (_root != null) _root.SetActive(false);
        _currentTargetId = null;
        var store = _lockedStore;
        _lockedStore = null;
        if (store == null) return;
        try { if (store.Pointer != IntPtr.Zero) store.OnMouseFlagChanged(); }
        catch (Exception ex) { _log?.Invoke($"[WARN] [NicokoboForge/Recipes] cursor release deferred: {ex.Message}"); }
    }

    internal static void ClearScene()
    {
        Close();
        if (_root != null) UnityEngine.Object.Destroy(_root);
        _root = _bookRoot = _hoverRoot = null;
        _pageHost = null;
        _paper = null;
        _font = null;
        _title = _issuer = _pageNumber = _hoverTitle = _hoverDescription = null;
        _hoverPaper = null;
        _left = _right = null;
        _screenWidth = _screenHeight = _page = _lastBrowsePage = 0;
        _pages = Array.Empty<RecipeGuidePage>();
        HoverTargets.Clear();
        Capture.Reset();
        RecipeGuideArtwork.ClearScene();
    }

    internal static void Uninstall()
    {
        ClearScene();
        if (_installed) NativeHookSet.Remove(HookId, _log);
        _installed = false;
    }

    private static void ShowPage(int page)
    {
        HideHover();
        if (_bookRoot == null || _pages.Count == 0) return;
        _page = Math.Clamp(page, 0, _pages.Count - 1);
        if (_currentTargetId == null) _lastBrowsePage = _page;
        for (int index = 0; index < _pages.Count; index++) _bookRoot.transform.GetChild(index).gameObject.SetActive(index == _page);
        _left!.interactable = _page > 0;
        _right!.interactable = _page < _pages.Count - 1;
        _pageNumber!.text = $"{_page + 1}/{_pages.Count}";
    }

    private static bool BlocksBackground() => Capture.BlocksPointer(Time.frameCount, PointerOverWindow, MouseHeld);
    private static bool InputPrefix(InputActionManager __instance)
    {
        if (PointerOverWindow && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)))
        {
            var drag = ItemMouseDragHandler.current;
            if (drag != null && drag.currentItem != null) drag.OnEventReset();
        }
        if (!BlocksBackground()) return true;
        __instance.Reset();
        __instance.lastMousePosition = Input.mousePosition;
        return false;
    }
    private static bool DoubleClickPrefix(CustomEventHandler __instance)
    {
        if (!BlocksBackground()) return true;
        __instance.lastHandlerClicked = null;
        __instance.hasRayCast = false;
        __instance.timeDeltaSeconds = __instance.doubleClickTimeThreshold;
        return false;
    }
    private static bool RaycastPrefix(StoreUIManager __instance)
    {
        if (!PointerOverWindow && !BlocksBackground()) return true;
        __instance.hoveringStoreUI = true;
        return false;
    }
    private static void StoreUpdatePostfix(StoreUIManager __instance)
    {
        if (PointerOverWindow || BlocksBackground()) __instance.hoveringStoreUI = true;
    }
    private static bool EscapePrefix(ref bool __result)
    {
        if (!Visible) return true;
        Close();
        __result = true;
        return false;
    }
}
