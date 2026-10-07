using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nicokobo.Forge;

internal static partial class RecipeGuideBrowser
{
    private static void EnsureWindow()
    {
        if (_root != null) return;
        _font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(candidate =>
            candidate.name == (_english ? "NotoSans-Medium SDF" : "NotoSansSC-VariableFont_wght"))
            ?? Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(candidate => candidate.name == "NotoSans-Medium SDF")
            ?? throw new InvalidOperationException("Game localization font unavailable");
        _root = Node("ForgeIllustratedGuide", null);
        _root.SetActive(false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        canvas.pixelPerfect = true;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1;
        _root.AddComponent<GraphicRaycaster>();
        var paperRoot = Node("ForgeGuidePaper", _root.transform);
        _paper = paperRoot.GetComponent<RectTransform>();
        Position(_paper, new Vector2(-300, 0), new Vector2(500, 660));
        var hitArea = paperRoot.AddComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        var parent = paperRoot.transform;
        Box(parent, "ForgeGuideShadow", new Vector2(5, -4), new Vector2(501, 641), new Color(0, 0, 0, .24f));
        RecipeGuideArtwork.DrawNotebook(parent, Paper);
        _issuer = Text(parent, "ForgeGuideIssuer", "", 18, new Vector2(5, 289), new Vector2(248, 30), TextAlignmentOptions.Center, Blue);
        _issuer.fontStyle = FontStyles.Bold;
        _title = Text(parent, "ForgeGuideTitle", "", 21, new Vector2(7, 258), new Vector2(414, 34), TextAlignmentOptions.Center, Ink);
        _title.fontStyle = FontStyles.Bold;
        _pageHost = Node("ForgeGuidePages", parent).transform;
        Position(_pageHost.GetComponent<RectTransform>(), new Vector2(9, -8), new Vector2(500, 550));
        _left = Control(parent, "ForgeGuidePrevious", "‹", new Vector2(-51, -299), new Vector2(42, 36), () => ShowPage(_page - 1));
        _right = Control(parent, "ForgeGuideNext", "›", new Vector2(74, -299), new Vector2(42, 36), () => ShowPage(_page + 1));
        _pageNumber = Text(parent, "ForgeGuidePageNumber", "", 21, new Vector2(12, -299), new Vector2(88, 32), TextAlignmentOptions.Center, Muted);
        _pageNumber.fontStyle = FontStyles.Italic;
        Control(parent, "ForgeGuideClose", "×", new Vector2(226, 304), new Vector2(36, 36), Close);
        RecipeGuideWindowDrag.Attach(paperRoot);
    }

    private static void BuildPage(Transform parent, RecipeGuidePage data, int index,
        List<HoverTarget> hoverTargets)
    {
        var page = Node("ForgeGuidePage:" + index, parent);
        Position(page.GetComponent<RectTransform>(), Vector2.zero, new Vector2(500, 550));
        var p = page.transform;
        var heading = Text(p, "ForgeGuideHeading", data.Title, 19, new Vector2(0, 218), new Vector2(414, 30), TextAlignmentOptions.Center, Blue);
        heading.fontStyle = FontStyles.Bold;
        Text(p, "ForgeGuideSubtitle", data.Subtitle, 13, new Vector2(0, 192), new Vector2(412, 24), TextAlignmentOptions.Center, Muted);
        Box(p, "RecipeHeadingRule", new Vector2(0, 174), new Vector2(412, 1), new Color(Blue.r, Blue.g, Blue.b, .35f));

        if (data.Recipes.Count == 0)
        {
            Text(p, "ForgeGuideEmpty", data.Note, 18, new Vector2(0, 0), new Vector2(400, 100),
                TextAlignmentOptions.Center, Muted);
            page.SetActive(false);
            return;
        }

        int rowCount = data.Recipes.Sum(recipe => recipe.Rows);
        float rowHeight = 414f / Math.Max(1, rowCount);
        int rowOffset = 0;
        for (int i = 0; i < data.Recipes.Count; i++)
        {
            var step = data.Recipes[i];
            int rows = step.Rows;
            float top = 158 - rowOffset * rowHeight;
            float center = top - rows * rowHeight / 2;
            bool hero = rowCount == 1;
            float iconSize = hero ? 126 : rowCount == 2 ? 85 : 54;
            float captionSize = hero ? 18 : 14;
            float figureLift = hero ? 0 : 16;
            bool oneIngredient = step.Inputs.Count == 1;
            float labelY = hero ? center + 124 : top - 8;
            var productName = Text(p, "RecipeProduct:" + step.RecipeId, step.Output.Caption, hero ? 18 : 15,
                new Vector2(-3, labelY), new Vector2(407, 24), TextAlignmentOptions.Left, Blue);
            AddHoverTarget(hoverTargets, productName.rectTransform, step.Output);

            for (int row = 0; row < rows; row++)
            {
                var inputs = step.Inputs.Skip(row * step.InputsPerRow).Take(step.InputsPerRow).ToArray();
                float y = top - (row + .5f) * rowHeight + figureLift;
                float stride = 279f / Math.Max(1, inputs.Length);
                for (int j = 0; j < inputs.Length; j++)
                {
                    float x = oneIngredient ? -144 : -211 + stride * (j + .5f);
                    float width = oneIngredient ? 140 : stride - 8;
                    DrawFigure(p, inputs[j], new Vector2(x, y), new Vector2(Math.Min(iconSize, width - 8), iconSize),
                        width, captionSize);
                    if (j < inputs.Length - 1)
                        Text(p, "IngredientPlus:" + j, "+", 20, new Vector2(-211 + stride * (j + 1), y),
                            new Vector2(18, 26), TextAlignmentOptions.Center, Ink);
                }
                if (row < rows - 1)
                    Text(p, "IngredientRowPlus:" + row, "+", 18, new Vector2(-72, y - rowHeight / 2),
                        new Vector2(22, 24), TextAlignmentOptions.Center, Ink);
            }
            RecipeGuideArtwork.Arrow(p, new Vector2(oneIngredient ? 3 : 99, center + figureLift), oneIngredient ? 164 : hero ? 67 : 48, Ink);
            DrawFigure(p, step.Output, new Vector2(oneIngredient ? 146 : 174, center + figureLift),
                new Vector2(oneIngredient && hero ? 126 : Math.Min(94, iconSize), iconSize),
                oneIngredient ? 140 : 96, captionSize, hoverTargets);
            if (i < data.Recipes.Count - 1)
            {
                float y = top - rows * rowHeight - 1;
                Box(p, "RecipeRule:" + i, new Vector2(0, y), new Vector2(410, 1), new Color(Muted.r, Muted.g, Muted.b, .23f));
            }
            rowOffset += rows;
        }
        Text(p, "ForgeGuideNote", data.Note, 12, new Vector2(0, -267), new Vector2(414, 30), TextAlignmentOptions.Center, Muted);
        page.SetActive(false);
    }

    private static void DrawFigure(Transform p, RecipeGuideFigure figure, Vector2 at,
        Vector2 iconSize, float width, float textSize, List<HoverTarget>? hoverTargets = null)
    {
        if (!RecipeGuideArtwork.DrawItem(p, figure.ItemId, at, iconSize))
        {
            Outline(p, "MissingFigure:" + figure.ItemId, at, new Vector2(32, 38), 2, Muted);
            Text(p, "MissingFigureLabel", "?", 25, at, new Vector2(32, 38), TextAlignmentOptions.Center, Muted);
        }
        bool chineseCaption = figure.Caption.Any(character => character > 127);
        float captionHeight = figure.Caption.Length > (chineseCaption ? 8 : 20) ? 48 : 32;
        float labelY = at.y - iconSize.y / 2 - captionHeight / 2;
        Text(p, "ForgeGuideCaption:" + figure.ItemId, figure.Caption + " " + (figure.QuantityText.Length > 0 ? figure.QuantityText : "×" + figure.Amount), textSize,
            new Vector2(at.x, labelY), new Vector2(width, captionHeight), TextAlignmentOptions.Top, Ink);
        if (!string.IsNullOrEmpty(figure.Requirement))
            Text(p, "ForgeGuideRequirement:" + figure.ItemId, figure.Requirement, 11,
                new Vector2(at.x, labelY - captionHeight / 2 - 12), new Vector2(width, 20), TextAlignmentOptions.Top, Red);
        if (hoverTargets != null)
        {
            float extra = string.IsNullOrEmpty(figure.Requirement) ? 0 : 22;
            var area = Node("RecipeOutputHover:" + figure.ItemId, p).GetComponent<RectTransform>();
            Position(area, at - new Vector2(0, (captionHeight + extra) / 2),
                new Vector2(width, iconSize.y + captionHeight + extra));
            AddHoverTarget(hoverTargets, area, figure);
        }
    }

    private static void AddHoverTarget(List<HoverTarget> targets, RectTransform area, RecipeGuideFigure figure)
    {
        string? description = ForgePresentationApi.GetRegisteredItemDescription(figure.ItemId, _english);
        if (!string.IsNullOrWhiteSpace(description)) targets.Add(new(area, figure.Caption, description));
    }

    private static void HideHover()
    {
        _hoverTarget = null;
        if (_hoverRoot != null) _hoverRoot.SetActive(false);
    }

    private static void UpdateHover()
    {
        if (!Visible ||
            Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            HideHover();
            return;
        }

        // Geometry-only hit areas preserve the book's existing click and drag routing.
        var target = HoverTargets.FirstOrDefault(candidate => RecipeGuideWindowDrag.ContainsPointer(candidate.Area.gameObject));
        if (target == null)
        {
            HideHover();
            return;
        }
        var canvasRect = _root!.GetComponent<RectTransform>();
        var mouse = Input.mousePosition;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                new Vector2(mouse.x, mouse.y), null, out var pointer))
        {
            HideHover();
            return;
        }
        if (_hoverRoot == null)
        {
            _hoverRoot = Box(_root.transform, "RecipeEffectTooltip", Vector2.zero, new Vector2(350, 100), Ink).gameObject;
            _hoverPaper = Box(_hoverRoot.transform, "RecipeEffectTooltipPaper", Vector2.zero, new Vector2(346, 96), Paper);
            _hoverTitle = Text(_hoverRoot.transform, "RecipeEffectTooltipTitle", "", 17,
                Vector2.zero, new Vector2(322, 26), TextAlignmentOptions.TopLeft, Blue);
            _hoverTitle.fontStyle = FontStyles.Bold;
            _hoverDescription = Text(_hoverRoot.transform, "RecipeEffectTooltipDescription", "", 15,
                Vector2.zero, new Vector2(322, 50), TextAlignmentOptions.TopLeft, Ink);
            // All tooltip graphics ignore raycasts, including outside the reading window.
        }
        if (_hoverTarget != target)
        {
            _hoverTarget = target;
            _hoverTitle!.font = _hoverDescription!.font = _font!;
            _hoverTitle.text = target.Title;
            _hoverDescription.text = target.Description;
        }
        var frame = canvasRect.rect;
        float width = Math.Min(350, frame.width - 24);
        float titleHeight = Math.Max(26, _hoverTitle!.GetPreferredValues(target.Title, width - 28, 0).y);
        float bodyHeight = _hoverDescription!.GetPreferredValues(target.Description, width - 28, 0).y;
        float height = Math.Min(frame.height - 24, titleHeight + bodyHeight + 34);
        Position(_hoverRoot.GetComponent<RectTransform>(), Vector2.zero, new Vector2(width, height));
        Position(_hoverPaper!.rectTransform, Vector2.zero, new Vector2(width - 4, height - 4));
        Position(_hoverTitle.rectTransform, new Vector2(0, height / 2 - 12 - titleHeight / 2),
            new Vector2(width - 28, titleHeight));
        Position(_hoverDescription.rectTransform, new Vector2(0, -titleHeight / 2 - 5),
            new Vector2(width - 28, height - titleHeight - 34));
        float x = pointer.x + 16 + width / 2;
        if (x + width / 2 > frame.xMax - 12) x = pointer.x - 16 - width / 2;
        _hoverRoot.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            Mathf.Clamp(x, frame.xMin + 12 + width / 2, frame.xMax - 12 - width / 2),
            Mathf.Clamp(pointer.y - height / 2, frame.yMin + 12 + height / 2, frame.yMax - 12 - height / 2));
        _hoverRoot.transform.SetAsLastSibling();
        _hoverRoot.SetActive(true);
    }

    internal static GameObject Node(string name, Transform? parent)
    {
        var node = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new[] { Il2CppType.Of<RectTransform>() }));
        node.hideFlags = HideFlags.DontSave;
        if (parent != null) node.transform.SetParent(parent, false);
        return node;
    }

    internal static void Position(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    internal static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, float angle = 0)
    {
        var node = Node(name, parent);
        node.AddComponent<CanvasRenderer>();
        var image = node.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        Position(image.rectTransform, position, size);
        image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        return image;
    }

    internal static void Outline(Transform p, string name, Vector2 at, Vector2 size, float line, Color color)
    {
        Box(p, name + ":top", at + new Vector2(0, size.y / 2), new Vector2(size.x, line), color);
        Box(p, name + ":bottom", at - new Vector2(0, size.y / 2), new Vector2(size.x, line), color);
        Box(p, name + ":left", at - new Vector2(size.x / 2, 0), new Vector2(line, size.y), color);
        Box(p, name + ":right", at + new Vector2(size.x / 2, 0), new Vector2(line, size.y), color);
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment, Color color)
    {
        var node = Node(name, parent);
        node.AddComponent<CanvasRenderer>();
        var label = node.AddComponent<TextMeshProUGUI>();
        label.font = _font!;
        label.fontWeight = FontWeight.Medium;
        label.fontSize = label.fontSizeMax = size;
        label.fontSizeMin = Math.Min(size, 11);
        label.enableAutoSizing = true;
        label.enableWordWrapping = true;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.text = text;
        Position(label.rectTransform, position, dimensions);
        return label;
    }

    private static Button Control(Transform parent, string name, string caption, Vector2 position, Vector2 size, Action click)
    {
        var image = Box(parent, name, position, size, new Color(1, 1, 1, 0));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = new Color(.6f, .6f, .6f, .32f);
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(click));
        Text(image.transform, name + "Label", caption, 31, Vector2.zero, size, TextAlignmentOptions.Center, Muted);
        return button;
    }
}
