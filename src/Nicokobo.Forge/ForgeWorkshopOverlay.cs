using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Nicokobo.Forge;

internal readonly record struct ForgeWorkshopUiCommand(
    string? TabId,
    string? SelectId,
    bool Purchase,
    bool Close,
    bool OpenWebsite = false);

// Independent IMGUI storefront. It borrows the visual hierarchy and muted
// palette of the Wilds Network page without reading, cloning or mutating any
// native Wilds UI objects.
internal static class ForgeWorkshopOverlay
{
    internal const string WebsiteUrl = "https://nicokobo.com";
    private const string WebsiteLabel = "nicokobo.com";
    private const string WebsiteLogoResource =
        "Nicokobo.Forge.Assets.Brand.nicokobo_logo.png";
    private const float DesignWidth = ForgeNumbers.Workshop.DesignWidth;
    private const float DesignHeight = ForgeNumbers.Workshop.DesignHeight;
    private const float WindowWidth = ForgeNumbers.Workshop.WindowWidth;
    private const float WindowHeight = ForgeNumbers.Workshop.WindowHeight;

    private static readonly Color Backdrop = new(0.035f, 0.027f, 0.035f, 0.78f);
    private static readonly Color Window = new(0.29f, 0.23f, 0.28f, 1f);
    private static readonly Color Header = new(0.235f, 0.19f, 0.23f, 1f);
    private static readonly Color Panel = new(0.36f, 0.30f, 0.32f, 1f);
    private static readonly Color Section = new(0.43f, 0.35f, 0.35f, 1f);
    private static readonly Color Node = new(0.26f, 0.24f, 0.27f, 1f);
    private static readonly Color NodeHover = new(0.34f, 0.30f, 0.33f, 1f);
    private static readonly Color NodeSelected = new(0.43f, 0.31f, 0.30f, 1f);
    private static readonly Color NodeLocked = new(0.20f, 0.19f, 0.21f, 1f);
    private static readonly Color Border = new(0.62f, 0.34f, 0.27f, 1f);
    private static readonly Color Accent = new(0.77f, 0.43f, 0.28f, 1f);
    private static readonly Color Text = new(0.90f, 0.86f, 0.80f, 1f);
    private static readonly Color Muted = new(0.70f, 0.66f, 0.63f, 1f);
    private static readonly Color Ready = new(0.68f, 0.78f, 0.58f, 1f);
    private static readonly Color Disabled = new(0.47f, 0.44f, 0.45f, 1f);

    private static Texture2D? _backdropTexture;
    private static Texture2D? _windowTexture;
    private static Texture2D? _headerTexture;
    private static Texture2D? _panelTexture;
    private static Texture2D? _sectionTexture;
    private static Texture2D? _nodeTexture;
    private static Texture2D? _nodeHoverTexture;
    private static Texture2D? _nodeSelectedTexture;
    private static Texture2D? _nodeLockedTexture;
    private static Texture2D? _borderTexture;
    private static Texture2D? _accentTexture;
    private static Texture2D? _websiteLogoTexture;
    private static readonly Dictionary<Texture2D, GUIStyle> FillStyles = new();
    private static GUIStyle? _titleStyle;
    private static GUIStyle? _websiteStyle;
    private static GUIStyle? _statStyle;
    private static GUIStyle? _detailTitleStyle;
    private static GUIStyle? _bodyStyle;
    private static GUIStyle? _mutedStyle;
    private static GUIStyle? _sectionStyle;
    private static GUIStyle? _nodeStyle;
    private static GUIStyle? _selectedNodeStyle;
    private static GUIStyle? _lockedNodeStyle;
    private static GUIStyle? _purchaseStyle;
    private static GUIStyle? _disabledPurchaseStyle;
    private static GUIStyle? _closeStyle;
    private static GUIStyle? _unlockedStateStyle;
    private static GUIStyle? _readyStateStyle;
    private static GUIStyle? _lockedStateStyle;
    private static GUIStyle? _costStyle;

    internal static ForgeWorkshopUiCommand HitTest(
        IReadOnlyList<ForgeWorkshopTabHeader> tabs, string activeTabId,
        ForgeWorkshopSnapshot snapshot, string? selectedId,
        float screenX, float screenY)
    {
        if (snapshot.Entries.Count == 0) return default;
        float scale = Scale();
        var window = WindowRect(scale);
        var mouse = new Vector2(screenX / scale,
            (Screen.height - screenY) / scale);
        if (!window.Contains(mouse)) return default;
        var header = new Rect(window.x + 3f, window.y + 3f,
            window.width - 6f, 52f);
        if (new Rect(header.xMax - 46f, header.y + 8f, 34f, 34f)
                .Contains(mouse))
            return new ForgeWorkshopUiCommand(null, null, false, true);
        if (WebsiteRect(header).Contains(mouse))
            return new ForgeWorkshopUiCommand(null, null, false, false,
                OpenWebsite: true);
        var tabBar = new Rect(window.x + 18f, header.yMax + 8f,
            window.width - 36f, 36f);
        int pageStart = TabPageStart(tabs, activeTabId);
        if (tabs.Count > ForgeNumbers.Workshop.VisibleTabs && new Rect(tabBar.x, tabBar.y, 34f, 36f)
                .Contains(mouse) && pageStart > 0)
            return new ForgeWorkshopUiCommand(tabs[pageStart - 1].Id,
                null, false, false);
        if (tabs.Count > ForgeNumbers.Workshop.VisibleTabs && new Rect(tabBar.xMax - 34f, tabBar.y,
                34f, 36f).Contains(mouse) && pageStart + ForgeNumbers.Workshop.VisibleTabs < tabs.Count)
            return new ForgeWorkshopUiCommand(tabs[pageStart + ForgeNumbers.Workshop.VisibleTabs].Id,
                null, false, false);
        for (int index = pageStart; index < Math.Min(pageStart + ForgeNumbers.Workshop.VisibleTabs, tabs.Count);
             index++)
            if (TabRect(tabBar, Math.Min(tabs.Count, ForgeNumbers.Workshop.VisibleTabs), index - pageStart,
                    tabs.Count > ForgeNumbers.Workshop.VisibleTabs).Contains(mouse))
                return new ForgeWorkshopUiCommand(tabs[index].Id,
                    null, false, false);
        float bodyY = tabBar.yMax + 8f;
        var left = new Rect(window.x + 18f, bodyY + 38f, 300f,
            window.yMax - bodyY - 56f);
        var selected = snapshot.Entries.FirstOrDefault(x => x.Id == selectedId)
            ?? snapshot.Entries[0];
        if (ForgeWorkshopApi.CanUnlock(selected) &&
            new Rect(left.x + 16f, left.yMax - 58f, left.width - 32f, 42f)
                .Contains(mouse))
            return new ForgeWorkshopUiCommand(null, null, true, false);
        var right = new Rect(left.xMax + 10f, left.y,
            window.xMax - left.xMax - 28f, left.height);
        var positions = NodeRects(right, snapshot.Groups, snapshot.Entries);
        foreach (var entry in snapshot.Entries)
            if (positions[entry.Id].Contains(mouse))
                return new ForgeWorkshopUiCommand(null, entry.Id, false, false);
        return default;
    }

    private static float Scale() => Mathf.Clamp(Mathf.Min(
        Screen.width / DesignWidth, Screen.height / DesignHeight), ForgeNumbers.Workshop.MinimumScale, 1f);

    private static Rect WindowRect(float scale) => new(
        (Screen.width / scale - WindowWidth) * 0.5f,
        (Screen.height / scale - WindowHeight) * 0.5f,
        WindowWidth, WindowHeight);

    private static Rect WebsiteRect(Rect header) => new(
        header.xMax - 230f, header.y + 9f, 160f, 34f);

    private static int TabPageStart(IReadOnlyList<ForgeWorkshopTabHeader> tabs,
        string activeTabId) => Math.Max(0, tabs.ToList().FindIndex(x =>
            x.Id == activeTabId) / ForgeNumbers.Workshop.VisibleTabs * ForgeNumbers.Workshop.VisibleTabs);

    private static Rect TabRect(Rect bar, int count, int index,
        bool paged = false)
    {
        const float gap = ForgeNumbers.Workshop.TabGap;
        float usable = bar.width - (paged ? 76f : 0f);
        float width = (usable - gap * (count - 1)) / count;
        return new Rect(bar.x + (paged ? 38f : 0f) + index * (width + gap), bar.y,
            width, bar.height);
    }

    internal static void Draw(
        IReadOnlyList<ForgeWorkshopTabHeader> tabs, string activeTabId,
        ForgeWorkshopSnapshot snapshot, string? selectedId)
    {
        if (snapshot.Entries.Count == 0) return;
        EnsureStyles();
        bool english = snapshot.English;

        float scale = Scale();
        float virtualWidth = Screen.width / scale;
        float virtualHeight = Screen.height / scale;
        var window = WindowRect(scale);
        var previousMatrix = GUI.matrix;
        int previousDepth = GUI.depth;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.depth = -1000;

        try
        {
            Fill(new Rect(0f, 0f, virtualWidth, virtualHeight),
                _backdropTexture!);
            DrawBordered(window, _windowTexture!, _borderTexture!, 3f);
            var header = new Rect(window.x + 3f, window.y + 3f,
                window.width - 6f, 52f);
            Fill(header, _headerTexture!);
            DrawWebsiteLogo(new Rect(header.x + 16f, header.y + 7f, 42f, 38f));
            GUI.Label(new Rect(header.x + 62f, header.y + 7f, 480f, 38f),
                english ? "NICO WORKSHOP" : "Nico工坊", _titleStyle!);
            var website = WebsiteRect(header);
            var pointer = Input.mousePosition;
            bool websiteHovered = website.Contains(new Vector2(pointer.x / scale,
                (Screen.height - pointer.y) / scale));
            _websiteStyle!.normal.textColor = websiteHovered ? Accent : Text;
            GUI.Label(website, WebsiteLabel, _websiteStyle);
            float linkWidth = _websiteStyle.CalcSize(new GUIContent(WebsiteLabel)).x;
            Fill(new Rect(website.xMax - linkWidth, website.yMax - 7f,
                linkWidth, websiteHovered ? 2f : 1f), _accentTexture!);
            GUI.Label(new Rect(header.xMax - 46f, header.y + 8f, 34f, 34f),
                "X", _closeStyle!);

            var tabBar = new Rect(window.x + 18f, header.yMax + 8f,
                window.width - 36f, 36f);
            int pageStart = TabPageStart(tabs, activeTabId);
            if (tabs.Count > ForgeNumbers.Workshop.VisibleTabs)
            {
                GUI.Label(new Rect(tabBar.x, tabBar.y, 34f, 36f), "<",
                    _nodeStyle!);
                GUI.Label(new Rect(tabBar.xMax - 34f, tabBar.y, 34f, 36f), ">",
                    _nodeStyle!);
            }
            for (int index = pageStart;
                 index < Math.Min(pageStart + ForgeNumbers.Workshop.VisibleTabs, tabs.Count); index++)
            {
                var tab = tabs[index];
                GUI.Label(TabRect(tabBar, Math.Min(tabs.Count, ForgeNumbers.Workshop.VisibleTabs),
                        index - pageStart, tabs.Count > ForgeNumbers.Workshop.VisibleTabs),
                    english ? tab.EnglishTitle : tab.ChineseTitle,
                    tab.Id == activeTabId ? _selectedNodeStyle! : _nodeStyle!);
            }
            float bodyY = tabBar.yMax + 8f;
            GUI.Label(new Rect(window.x + 18f, bodyY, 188f, 30f),
                english ? $"Credits  {snapshot.Credits:N0}" :
                    $"信用点  {snapshot.Credits:N0}",
                _statStyle!);

            var left = new Rect(window.x + 18f, bodyY + 38f, 300f,
                window.yMax - bodyY - 56f);
            var right = new Rect(left.xMax + 10f, left.y,
                window.xMax - left.xMax - 28f, left.height);
            DrawBordered(left, _panelTexture!, _borderTexture!, 2f);
            DrawBordered(right, _panelTexture!, _borderTexture!, 2f);

            var selected = snapshot.Entries.FirstOrDefault(x => x.Id == selectedId)
                ?? snapshot.Entries[0];
            DrawDetails(left, selected, snapshot.Message, english);
            DrawGraph(right, snapshot.Groups, snapshot.Entries,
                selected.Id, english);

            var current = Event.current;
            if (current != null && (current.isMouse || current.type == EventType.ScrollWheel))
                current.Use();
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }
    }

    private static void DrawDetails(Rect area, ForgeWorkshopEntry selected,
        string message, bool english)
    {
        float x = area.x + 16f;
        float width = area.width - 32f;
        GUI.Label(new Rect(x, area.y + 14f, width, 34f), selected.Title,
            _detailTitleStyle!);
        GUI.Label(new Rect(x, area.y + 50f, width, 22f), selected.Subtitle,
            _mutedStyle!);
        Fill(new Rect(x, area.y + 79f, width, 2f), _borderTexture!);
        GUI.Label(new Rect(x, area.y + 94f, width, 96f), selected.Description,
            _bodyStyle!);

        DrawInfoBox(new Rect(x, area.y + 202f, width, 46f), selected.Requirement);
        DrawCostBox(new Rect(x, area.y + 256f, width, 172f), selected,
            english);

        var stateStyle = selected.Unlocked ? _unlockedStateStyle!
            : selected.Completed == true || selected.Completed == null && selected.PrerequisiteMet ? _readyStateStyle! : _lockedStateStyle!;
        GUI.Label(new Rect(x, area.y + 433f, width, 34f), selected.StateText,
            stateStyle);

        if (!string.IsNullOrWhiteSpace(message))
        {
            GUI.Label(new Rect(x, area.y + 157f, width, 39f),
                message, _bodyStyle!);
        }

        GUI.Label(new Rect(x, area.yMax - 58f, width, 42f),
            selected.ActionText, ForgeWorkshopApi.CanUnlock(selected)
                ? _purchaseStyle! : _disabledPurchaseStyle!);
    }

    private static void DrawGraph(Rect area,
        IReadOnlyList<ForgeWorkshopGroup> groups,
        IReadOnlyList<ForgeWorkshopEntry> entries, string selectedId,
        bool english)
    {
        var positions = NodeRects(area, groups, entries);
        GUI.Label(new Rect(area.x + 12f, area.y + 8f, area.width - 24f, 25f),
            english ? "PROGRESSION MAP" : "解锁星图", _sectionStyle!);
        foreach (var entry in entries)
        {
            foreach (var dependency in entry.Dependencies.Concat(entry.VisualLinks).Distinct(StringComparer.Ordinal))
            {
                if (positions.TryGetValue(dependency, out var source))
                    DrawLink(source.center, positions[entry.Id].center,
                        entry.Completed ?? (entry.PrerequisiteMet || entry.Unlocked));
            }
        }
        foreach (var entry in entries)
        {
            var style = entry.Id == selectedId ? _selectedNodeStyle!
                : !entry.PrerequisiteMet && !entry.Unlocked
                    ? _lockedNodeStyle! : _nodeStyle!;
            string prefix = entry.Unlocked
                ? (english ? "DONE" : entry.Completed.HasValue ? "已领取" : "已解锁")
                : entry.Completed == true
                    ? (english ? "CLAIM" : "待领取")
                : !entry.PrerequisiteMet
                    ? (english ? "LOCKED" : "未满足")
                    : entry.NodeCostText;
            GUI.Label(positions[entry.Id], $"* {entry.Title}\n{prefix}", style);
        }
    }

    private static Dictionary<string, Rect> NodeRects(Rect area,
        IReadOnlyList<ForgeWorkshopGroup> groups,
        IReadOnlyList<ForgeWorkshopEntry> entries)
    {
        var result = new Dictionary<string, Rect>(StringComparer.Ordinal);
        int entryIndex = 0;
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            int count = groups[groupIndex].EntryCount;
            for (int index = 0; index < count; index++, entryIndex++)
            {
                var entry = entries[entryIndex];
                float angle = -Mathf.PI / 2f +
                    2f * Mathf.PI * groupIndex / groups.Count;
                float radius = 0.35f + index * (0.55f / Math.Max(1, count - 1));
                float offset = count > 2 ? (index % 2 == 0 ? -0.24f : 0.24f)
                    : 0f;
                float x = entry.GraphX ??
                    Mathf.Cos(angle) * radius - Mathf.Sin(angle) * offset;
                float y = entry.GraphY ??
                    Mathf.Sin(angle) * radius + Mathf.Cos(angle) * offset;
                const float nodeWidth = ForgeNumbers.Workshop.NodeWidth;
                const float nodeHeight = ForgeNumbers.Workshop.NodeHeight;
                float centerX = area.center.x + x * (area.width / 2f - 70f);
                float centerY = area.center.y + 15f +
                    y * (area.height / 2f - 66f);
                result.Add(entry.Id, new Rect(centerX - nodeWidth / 2f,
                    centerY - nodeHeight / 2f, nodeWidth, nodeHeight));
            }
        }
        return result;
    }

    private static void DrawLink(Vector2 from, Vector2 to, bool ready)
    {
        int steps = Math.Max(8, Mathf.CeilToInt(Vector2.Distance(from, to) / 10f));
        for (int index = 0; index <= steps; index++)
        {
            float t = index / (float)steps;
            var point = Vector2.Lerp(from, to, t);
            Fill(new Rect(point.x - 2f, point.y - 2f, 4f, 4f),
                ready ? _accentTexture! : _borderTexture!);
        }
    }

    private static void DrawInfoBox(Rect rect, string text)
    {
        DrawBordered(rect, _headerTexture!, _borderTexture!, 1f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 5f,
            rect.width - 16f, rect.height - 10f), text, _bodyStyle!);
    }

    private static void DrawCostBox(Rect rect, ForgeWorkshopEntry selected,
        bool english)
    {
        DrawBordered(rect, _headerTexture!, _borderTexture!, 1f);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 5f,
            rect.width - 16f, rect.height - 10f),
            ResourceText(selected, english), _costStyle!);
    }

    private static string ResourceText(ForgeWorkshopEntry entry, bool english)
    {
        if (entry.Conditions.Count == 0 && entry.Rewards.Count == 0)
            return entry.CostText;
        if (entry.Completed.HasValue)
            return entry.CostText + "\n" + (english ? "Rewards" : "奖励") + "\n" + string.Join("\n", entry.Rewards.Select(reward =>
                $"+ {ResourceName(reward.Kind, reward.ResourceId, reward.DisplayName, english)} ×{reward.Quantity:N0}"));
        var lines = new List<string> { english ? "Requirements" : "解锁条件" };
        foreach (var condition in entry.Conditions)
            lines.Add($"{(condition.Satisfied ? "✓" : "○")} " +
                $"{ResourceName(condition.Kind, condition.ResourceId, condition.DisplayName, english)} ×{condition.Quantity:N0}" +
                (condition.Consume
                    ? (english ? " [consume]" : " [消耗]")
                    : (english ? " [keep]" : " [持有]")));
        if (entry.Rewards.Count > 0)
            lines.Add(english ? "Rewards" : "奖励");
        foreach (var reward in entry.Rewards)
            lines.Add($"+ {ResourceName(reward.Kind, reward.ResourceId, reward.DisplayName, english)} " +
                $"×{reward.Quantity:N0}");
        return string.Join("\n", lines);
    }

    private static string ResourceName(ForgeWorkshopResourceKind kind,
        string id, string? displayName, bool english)
        => kind == ForgeWorkshopResourceKind.Credits
            ? (english ? "Credits" : "信用点")
            : string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    private static void DrawBordered(Rect rect, Texture2D fill,
        Texture2D border, float thickness)
    {
        Fill(rect, border);
        Fill(new Rect(rect.x + thickness, rect.y + thickness,
            rect.width - thickness * 2f, rect.height - thickness * 2f), fill);
    }

    private static void DrawWebsiteLogo(Rect rect)
    {
        var logo = _websiteLogoTexture!;
        float scale = Mathf.Min(rect.width / logo.width, rect.height / logo.height);
        float width = logo.width * scale;
        float height = logo.height * scale;
        Fill(new Rect(rect.center.x - width / 2f, rect.center.y - height / 2f,
            width, height), logo);
    }

    private static Texture2D LoadWebsiteLogo()
    {
        using var stream = typeof(ForgeWorkshopOverlay).Assembly
            .GetManifestResourceStream(WebsiteLogoResource)
            ?? throw new InvalidOperationException("Embedded website logo missing");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            name = "nico_website_logo",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        try
        {
            if (!ImageConversion.LoadImage(texture,
                    new Il2CppStructArray<byte>(memory.ToArray()), true))
                throw new InvalidOperationException("Embedded website logo could not be decoded");
            return texture;
        }
        catch
        {
            UnityEngine.Object.Destroy(texture);
            throw;
        }
    }

    // This game's IL2CPP build strips the GUI.DrawTexture overload chain.
    // GUI.Box with a cached background style is used by the existing status UI.
    private static void Fill(Rect rect, Texture2D texture)
        => GUI.Box(rect, string.Empty, FillStyles[texture]);

    private static void EnsureStyles()
    {
        if (_titleStyle != null) return;
        _websiteLogoTexture = LoadWebsiteLogo();
        _backdropTexture = Solid(Backdrop, "nico_backdrop");
        _windowTexture = Solid(Window, "nico_window");
        _headerTexture = Solid(Header, "nico_header");
        _panelTexture = Solid(Panel, "nico_panel");
        _sectionTexture = Solid(Section, "nico_section");
        _nodeTexture = Solid(Node, "nico_node");
        _nodeHoverTexture = Solid(NodeHover, "nico_node_hover");
        _nodeSelectedTexture = Solid(NodeSelected, "nico_node_selected");
        _nodeLockedTexture = Solid(NodeLocked, "nico_node_locked");
        _borderTexture = Solid(Border, "nico_border");
        _accentTexture = Solid(Accent, "nico_accent");
        foreach (var texture in new[] { _backdropTexture, _windowTexture,
                     _headerTexture, _panelTexture, _sectionTexture,
                     _nodeTexture, _nodeHoverTexture, _nodeSelectedTexture,
                     _nodeLockedTexture, _borderTexture, _accentTexture, _websiteLogoTexture })
        {
            var style = new GUIStyle(GUI.skin.label);
            style.border = new RectOffset(0, 0, 0, 0);
            style.normal.background = texture;
            FillStyles.Add(texture!, style);
        }

        _titleStyle = Label(ForgeNumbers.Workshop.TitleFontSize, Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
        _websiteStyle = Label(20, Text, FontStyle.Normal, TextAnchor.MiddleRight);
        _statStyle = Label(ForgeNumbers.Workshop.StatFontSize, Text, FontStyle.Normal, TextAnchor.MiddleCenter);
        _statStyle.normal.background = _sectionTexture;
        _detailTitleStyle = Label(ForgeNumbers.Workshop.DetailTitleFontSize, Accent, FontStyle.Bold,
            TextAnchor.MiddleLeft);
        _bodyStyle = Label(ForgeNumbers.Workshop.BodyFontSize, Text, FontStyle.Normal, TextAnchor.UpperLeft);
        _bodyStyle.wordWrap = true;
        _costStyle = Label(ForgeNumbers.Workshop.CostFontSize, Text, FontStyle.Normal, TextAnchor.UpperLeft);
        _costStyle.wordWrap = true;
        _mutedStyle = Label(ForgeNumbers.Workshop.MutedFontSize, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
        _sectionStyle = Label(ForgeNumbers.Workshop.SectionFontSize, Text, FontStyle.Bold, TextAnchor.MiddleLeft);

        _nodeStyle = Button(_nodeTexture, _nodeHoverTexture, Text);
        _selectedNodeStyle = Button(_nodeSelectedTexture, _nodeHoverTexture,
            new Color(1f, 0.88f, 0.72f, 1f));
        _lockedNodeStyle = Button(_nodeLockedTexture, _nodeHoverTexture, Disabled);
        _purchaseStyle = Button(_accentTexture, _nodeHoverTexture, Text);
        _purchaseStyle.fontSize = ForgeNumbers.Workshop.PurchaseFontSize;
        _disabledPurchaseStyle = Button(_nodeLockedTexture, _nodeLockedTexture,
            Disabled);
        _disabledPurchaseStyle.fontSize = ForgeNumbers.Workshop.PurchaseFontSize;
        _closeStyle = Button(_nodeTexture, _nodeHoverTexture, Text);
        _closeStyle.fontSize = ForgeNumbers.Workshop.CloseFontSize;
        _unlockedStateStyle = Label(ForgeNumbers.Workshop.StateFontSize, Ready, FontStyle.Bold,
            TextAnchor.MiddleCenter);
        _readyStateStyle = Label(ForgeNumbers.Workshop.StateFontSize, Accent, FontStyle.Bold,
            TextAnchor.MiddleCenter);
        _lockedStateStyle = Label(ForgeNumbers.Workshop.StateFontSize, Muted, FontStyle.Bold,
            TextAnchor.MiddleCenter);
    }

    private static GUIStyle Label(int size, Color color, FontStyle fontStyle,
        TextAnchor alignment)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = fontStyle,
            alignment = alignment,
            clipping = TextClipping.Clip
        };
        style.normal.textColor = color;
        return style;
    }

    private static GUIStyle Button(Texture2D normal, Texture2D hover,
        Color textColor)
    {
        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize = ForgeNumbers.Workshop.ButtonFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            clipping = TextClipping.Clip,
            padding = new RectOffset(5, 5, 4, 4)
        };
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = hover;
        style.focused.background = normal;
        style.normal.textColor = textColor;
        style.hover.textColor = textColor;
        style.active.textColor = textColor;
        style.focused.textColor = textColor;
        return style;
    }

    private static Texture2D Solid(Color color, string name)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(false, true);
        return texture;
    }
}
