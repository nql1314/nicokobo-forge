using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nicokobo.Forge.Runtime;

// Keep the native cards together under SidePanel so native button references
// and other injected cards continue to use the same content transform.
internal sealed class StartSelectionList
{
    private readonly RectTransform _content;
    private readonly RectTransform _frame;
    private readonly RectTransform _viewport;
    private readonly VerticalLayoutGroup _layout;
    private readonly ScrollRect _scroll;
    private readonly Scrollbar _scrollbar;
    private float _lastWidth = -1f;
    private float _lastHeight = -1f;
    private int _lastChildCount = -1;
    private bool _wasVisible;

    internal Transform Content => _content;

    internal bool IsAlive => _content != null && _frame != null;

    internal void MenuOpened() => _wasVisible = false;

    internal StartSelectionList(Transform side)
    {
        _content = side.gameObject.GetComponent<RectTransform>()
            ?? throw new InvalidOperationException("Start selection panel has no RectTransform");
        _layout = side.gameObject.GetComponent<VerticalLayoutGroup>()
            ?? throw new InvalidOperationException("Start selection panel has no vertical layout");
        var parent = side.parent
            ?? throw new InvalidOperationException("Start selection panel has no parent");
        int siblingIndex = side.GetSiblingIndex();
        var frameObject = Node("NicokoboForgeStartSelection", parent);
        _frame = frameObject.GetComponent<RectTransform>();
        CopyRect(_content, _frame);
        try
        {
            var viewportObject = Node("Viewport", _frame);
            _viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(_viewport);
            var hitArea = viewportObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            viewportObject.AddComponent<RectMask2D>();

            var scrollbarObject = Node("Scrollbar", _frame);
            var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(.5f, .5f);
            scrollbarRect.anchoredPosition = new Vector2(
                -ForgeNumbers.StartSelection.ScrollbarWidth / 2f - ForgeNumbers.StartSelection.ScrollbarInset, 0f);
            scrollbarRect.sizeDelta = new Vector2(ForgeNumbers.StartSelection.ScrollbarWidth,
                -2f * ForgeNumbers.StartSelection.ScrollbarVerticalPadding);
            var track = scrollbarObject.AddComponent<Image>();
            track.color = new Color(.55f, .75f, .74f, .16f);
            _scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            var handleObject = Node("Handle", scrollbarRect);
            var handleRect = handleObject.GetComponent<RectTransform>();
            Stretch(handleRect);
            var handle = handleObject.AddComponent<Image>();
            handle.color = new Color(.55f, .75f, .74f, .9f);
            _scrollbar.handleRect = handleRect;
            _scrollbar.targetGraphic = handle;
            _scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scrollbarObject.SetActive(false);

            _scroll = frameObject.AddComponent<ScrollRect>();
            _scroll.enabled = false;
            _scroll.content = _content;
            _scroll.viewport = _viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.verticalScrollbar = _scrollbar;

            _content.SetParent(_viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, _frame.rect.height);
            _content.localScale = Vector3.one;
            _content.localRotation = Quaternion.identity;
            _frame.SetSiblingIndex(siblingIndex);
            _scroll.enabled = true;
        }
        catch
        {
            _content.SetParent(parent, false);
            _content.SetSiblingIndex(siblingIndex);
            CopyRect(_frame, _content);
            UnityEngine.Object.Destroy(frameObject);
            throw;
        }
    }

    internal void Update()
    {
        if (_content == null || _frame == null) return;
        if (!_content.gameObject.activeInHierarchy)
        {
            _wasVisible = false;
            return;
        }
        float height = _frame.rect.height;
        float width = _frame.rect.width;
        if (height <= 0f || width <= 0f) return;
        if (_wasVisible && _lastChildCount == _content.childCount &&
            Mathf.Approximately(_lastHeight, height) && Mathf.Approximately(_lastWidth, width))
            return;

        float position = _wasVisible && _scrollbar.gameObject.activeSelf
            ? _scroll.verticalNormalizedPosition : 1f;
        var rows = new List<RectTransform>();
        for (int i = 0; i < _content.childCount; i++)
        {
            var child = _content.GetChild(i).gameObject;
            if (!child.activeSelf) continue;
            var rect = child.GetComponent<RectTransform>();
            var element = child.GetComponent<LayoutElement>();
            if (rect != null && (element == null || !element.isActiveAndEnabled || !element.ignoreLayout))
                rows.Add(rect);
        }

        float gaps = _layout.spacing * Math.Max(0, rows.Count - 1);
        float padding = _layout.padding.top + _layout.padding.bottom;
        float rowHeight = rows.Count == 0 ? ForgeNumbers.StartSelection.MaximumRowHeight :
            Mathf.Clamp((height - padding - gaps) / rows.Count,
                ForgeNumbers.StartSelection.MinimumRowHeight, ForgeNumbers.StartSelection.MaximumRowHeight);
        foreach (var row in rows)
        {
            row.sizeDelta = new Vector2(row.sizeDelta.x, rowHeight);
            LayoutRowContents(row, rowHeight);
        }
        _layout.childControlHeight = false;
        _layout.childForceExpandHeight = false;
        _layout.childControlWidth = true;
        _layout.childForceExpandWidth = true;
        _layout.childAlignment = TextAnchor.UpperLeft;

        float contentHeight = padding + gaps + rows.Count * rowHeight;
        bool overflow = contentHeight > height + .5f;
        _scrollbar.gameObject.SetActive(overflow);
        _viewport.offsetMax = new Vector2(overflow ? -ForgeNumbers.StartSelection.ScrollbarSpace : 0f, 0f);
        _content.sizeDelta = new Vector2(0f, Mathf.Max(height, contentHeight));
        _scroll.scrollSensitivity = rowHeight + _layout.spacing;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_frame);
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = overflow ? position : 1f;

        _lastWidth = width;
        _lastHeight = height;
        _lastChildCount = _content.childCount;
        _wasVisible = true;
    }

    private static void LayoutRowContents(RectTransform row, float rowHeight)
    {
        float iconSize = Mathf.Min(ForgeNumbers.StartSelection.IconSize,
            rowHeight - 2f * ForgeNumbers.StartSelection.RowVerticalPadding);
        float textLeft = ForgeNumbers.StartSelection.IconLeftPadding + iconSize +
            ForgeNumbers.StartSelection.IconTextGap;
        for (int i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i).gameObject;
            var icon = child.GetComponent<Image>();
            if (icon != null)
            {
                // Native icons use a center anchor and -103 X at width 258.
                // Widening the row moves that icon into the stretched text.
                var rect = icon.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(iconSize, iconSize);
                rect.anchoredPosition = new Vector2(
                    ForgeNumbers.StartSelection.IconLeftPadding + iconSize / 2f, 0f);
                icon.preserveAspect = true;
                continue;
            }
            var label = child.GetComponent(Il2CppType.Of<TextMeshProUGUI>())?.TryCast<TextMeshProUGUI>();
            if (label == null) continue;
            var textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.offsetMin = new Vector2(textLeft, ForgeNumbers.StartSelection.RowVerticalPadding);
            textRect.offsetMax = new Vector2(-ForgeNumbers.StartSelection.TextRightPadding,
                -ForgeNumbers.StartSelection.RowVerticalPadding);
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    private static GameObject Node(string name, Transform parent)
    {
        var node = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(
            new[] { Il2CppType.Of<RectTransform>() }));
        node.hideFlags = HideFlags.DontSave;
        node.layer = parent.gameObject.layer;
        node.transform.SetParent(parent, false);
        return node;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition = source.anchoredPosition;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }
}
