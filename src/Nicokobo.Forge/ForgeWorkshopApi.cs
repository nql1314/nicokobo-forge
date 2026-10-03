using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Workshop;
using UnityEngine;

namespace Nicokobo.Forge;

public sealed record ForgeWorkshopGroup(string ChineseTitle, string EnglishTitle,
    int EntryCount);
public enum ForgeWorkshopResourceKind { Credits, Item }

/// <summary>A condition checked by the content provider in the current run.
/// Consume describes the provider's unlock transaction, not a Forge transfer.</summary>
public sealed record ForgeWorkshopRequirement(ForgeWorkshopResourceKind Kind,
    string ResourceId, int Quantity, bool Consume, bool Satisfied)
{
    public string? DisplayName { get; init; }
}

/// <summary>A grant made exactly once by the content provider after unlock.</summary>
public sealed record ForgeWorkshopReward(ForgeWorkshopResourceKind Kind,
    string ResourceId, int Quantity)
{
    public string? DisplayName { get; init; }
}

public sealed record ForgeWorkshopEntry(string Id, string Title, string Subtitle,
    string Description, string Requirement, string CostText, string NodeCostText,
    string StateText, string ActionText, bool Unlocked, bool PrerequisiteMet,
    bool ActionEnabled)
{
    /// <summary>Stable IDs of prerequisite nodes. IDs from another chain are allowed.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    /// <summary>All conditions must be satisfied. The provider rechecks them at unlock.</summary>
    public IReadOnlyList<ForgeWorkshopRequirement> Conditions { get; init; } = [];
    public IReadOnlyList<ForgeWorkshopReward> Rewards { get; init; } = [];
    /// <summary>Optional achievement state. Unlocked remains the claimed/unlocked state.</summary>
    public bool? Completed { get; init; }
    /// <summary>Thematic links only; these never gate an action.</summary>
    public IReadOnlyList<string> VisualLinks { get; init; } = [];
    /// <summary>Optional graph coordinates in [-1, 1]. Omit both for radial layout.</summary>
    public float? GraphX { get; init; }
    public float? GraphY { get; init; }
}
public sealed record ForgeWorkshopSnapshot(int Credits, bool English,
    IReadOnlyList<ForgeWorkshopGroup> Groups,
    IReadOnlyList<ForgeWorkshopEntry> Entries, string Message = "");
public sealed record ForgeWorkshopTabHeader(string Id, string ChineseTitle,
    string EnglishTitle);

/// <summary>Shared Nico Workshop window. Content Mods own eligibility, payments,
/// persistence and effects; Forge owns tabs, input and presentation.</summary>
public static class ForgeWorkshopApi
{
    private sealed record Tab(string OwnerId, ForgeWorkshopTabHeader Header,
        Func<ForgeWorkshopSnapshot?> Snapshot, Action<string> Unlock);
    private static readonly Dictionary<string, Tab> Tabs = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Selections = new(StringComparer.Ordinal);
    private static readonly List<Tab> Available = [];
    private static readonly List<ForgeWorkshopTabHeader> Headers = [];
    private static Action<string>? _log;
    private static bool _enabled;
    private static bool _visible;
    private static bool _cursorCaptured;
    private static readonly ForgeWindowInputCapture InputCapture = new();
    private static CursorLockMode _previousCursorLock;
    private static bool _previousCursorVisible;
    private static DateTime _nextRefreshUtc;
    private static string? _activeTabId;
    private static string? _defaultChainId;
    private static ForgeWorkshopSnapshot? _activeSnapshot;
    private static string? _lastClickedEntryId;
    private static DateTime _lastEntryClickUtc;

    public static bool IsVisible => _visible;
    private static bool MouseHeld => Input.GetMouseButton(0) ||
        Input.GetMouseButton(1) || Input.GetMouseButton(2);
    internal static bool BlocksNativeInput => InputCapture.BlocksPointer(Time.frameCount,
        _visible && ForgeWorkshopOverlay.ContainsPointer(Input.mousePosition), MouseHeld);

    public static SubmitResult RegisterTab(string ownerId, string tabId,
        string chineseTitle, string englishTitle,
        Func<ForgeWorkshopSnapshot?> snapshot, Action<string> unlock)
    {
        if (string.IsNullOrWhiteSpace(ownerId) ||
            string.IsNullOrWhiteSpace(tabId) ||
            !tabId.StartsWith(ownerId + ".", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(chineseTitle) || chineseTitle.Length > ForgeNumbers.Workshop.MaxTitleChars ||
            string.IsNullOrWhiteSpace(englishTitle) || englishTitle.Length > ForgeNumbers.Workshop.MaxTitleChars ||
            snapshot == null || unlock == null)
            return new(SubmitStatus.Invalid, "Invalid workshop tab registration");
        if (Tabs.TryGetValue(tabId, out var existing))
            return new(existing.OwnerId == ownerId
                ? SubmitStatus.AlreadyPresent : SubmitStatus.Conflict,
                "Workshop tab ID is already registered");
        if (Tabs.Count >= ForgeNumbers.Workshop.MaxTabs)
            return new(SubmitStatus.Invalid, $"Workshop supports at most {ForgeNumbers.Workshop.MaxTabs} chains");
        Tabs.Add(tabId, new Tab(ownerId,
            new(tabId, chineseTitle, englishTitle), snapshot, unlock));
        SafeLog($"[NicokoboForge/Workshop] tab={tabId}; owner={ownerId}; status=Accepted");
        return new(SubmitStatus.Accepted, "Workshop tab registered");
    }

    /// <summary>Register an independently owned progression chain. A base-game
    /// chain, a Mod chain and an optional shared goal chain use the same API.</summary>
    public static SubmitResult RegisterChain(string ownerId, string chainId,
        string chineseTitle, string englishTitle,
        Func<ForgeWorkshopSnapshot?> snapshot, Action<string> unlock)
        => RegisterTab(ownerId, chainId, chineseTitle, englishTitle,
            snapshot, unlock);

    /// <summary>Open a tab if its provider is available in the current game.</summary>
    public static bool OpenTab(string tabId)
    {
        if (!_enabled) return false;
        RefreshTabs();
        if (!Available.Any(x => x.Header.Id == tabId)) return false;
        Open(tabId);
        return _visible;
    }

    /// <summary>Close the workshop, for example when a content Mod loads another save.</summary>
    public static void CloseWindow() => Close();

    internal static void SetDefaultChain(string chainId) => _defaultChainId = chainId;
    internal static void ResetRunSelection()
    {
        Close(); InputCapture.Reset(); ForgeWorkshopInputShield.Hide();
        _activeTabId = _defaultChainId; Selections.Clear();
    }

    internal static void Configure(bool enabled, Action<string> log)
    {
        _log = log;
        _enabled = enabled && ForgeWorkshopInputShield.Install(SafeLog);
        if (!_enabled) Close();
        SafeLog($"[NicokoboForge/Workshop] enabled={_enabled}");
    }

    internal static void Update()
    {
        if (!_visible && !BlocksNativeInput) ForgeWorkshopInputShield.Hide();
        if (!_enabled) return;
        if (Application.isFocused && Input.GetKeyDown(KeyCode.N))
        {
            if (_visible) Close();
            else
            {
                RefreshTabs();
                if (Available.Count > 0)
                    Open(Available.Any(x => x.Header.Id == _activeTabId)
                        ? _activeTabId! : Available[0].Header.Id);
            }
            return;
        }
        if (!_visible) return;
        if (!ForgeWorkshopInputShield.Show(SafeLog)) { Close(); return; }
        if (Application.isFocused && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (DateTime.UtcNow >= _nextRefreshUtc)
        {
            _nextRefreshUtc = DateTime.UtcNow.AddMilliseconds(ForgeNumbers.Workshop.RefreshMilliseconds);
            RefreshTabs();
            if (_activeSnapshot == null) { Close(); return; }
        }
        if (!Application.isFocused || !Input.GetMouseButtonDown(0) ||
            _activeSnapshot == null) return;
        var mouse = Input.mousePosition;
        var command = ForgeWorkshopOverlay.HitTest(Headers, _activeTabId!,
            _activeSnapshot, SelectedId(), mouse.x, mouse.y);
        if (command.Close)
        {
            SafeLog("[NicokoboForge/Workshop] close clicked");
            Close();
            return;
        }
        if (command.OpenWebsite)
        {
            _lastClickedEntryId = null;
            try
            {
                Application.OpenURL(ForgeWorkshopOverlay.WebsiteUrl);
                SafeLog($"[NicokoboForge/Workshop] browser launch requested; " +
                    $"url={ForgeWorkshopOverlay.WebsiteUrl}");
            }
            catch (Exception ex)
            {
                SafeLog($"[ERROR] [NicokoboForge/Workshop] website unavailable; " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
            return;
        }
        if (command.TabId is { } tabId)
        {
            _activeTabId = tabId;
            _lastClickedEntryId = null;
            _nextRefreshUtc = DateTime.UtcNow;
            RefreshTabs();
            SafeLog($"[NicokoboForge/Workshop] tab selected; id={tabId}");
            return;
        }
        if (command.SelectId is { } entryId)
        {
            bool doubleClick = _lastClickedEntryId == entryId &&
                DateTime.UtcNow - _lastEntryClickUtc <=
                TimeSpan.FromMilliseconds(ForgeNumbers.Workshop.ToggleCooldownMilliseconds);
            Selections[_activeTabId!] = entryId;
            _lastClickedEntryId = doubleClick ? null : entryId;
            _lastEntryClickUtc = DateTime.UtcNow;
            SafeLog($"[NicokoboForge/Workshop] selected; tab={_activeTabId}; " +
                $"entry={entryId}; doubleClick={doubleClick}");
            if (doubleClick && CanUnlock(_activeSnapshot.Entries.First(x =>
                    x.Id == entryId)))
                Unlock(entryId);
            return;
        }
        if (command.Purchase && SelectedId() is { } selected)
        {
            _lastClickedEntryId = null;
            Unlock(selected);
        }
    }

    internal static void Draw()
    {
        if (!_visible || _activeSnapshot == null) return;
        try
        {
            ForgeWorkshopOverlay.Draw(Headers, _activeTabId!,
                _activeSnapshot, SelectedId());
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Workshop] drawing unavailable; " +
                $"{ex.GetType().Name}: {ex.Message}");
            Close();
        }
    }

    private static void Unlock(string entryId)
    {
        var tab = Available.FirstOrDefault(x => x.Header.Id == _activeTabId);
        if (tab == null || _activeSnapshot?.Entries.FirstOrDefault(x =>
                x.Id == entryId) is not { } entry || !CanUnlock(entry)) return;
        try
        {
            tab.Unlock(entryId);
            SafeLog($"[NicokoboForge/Workshop] unlock invoked; tab={_activeTabId}; " +
                $"entry={entryId}");
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Workshop] unlock failed; tab={_activeTabId}; " +
                $"entry={entryId}; {ex.GetType().Name}: {ex.Message}");
        }
        _nextRefreshUtc = DateTime.UtcNow;
        RefreshTabs();
    }

    private static void Open(string tabId)
    {
        if (!_cursorCaptured)
        {
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _cursorCaptured = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _visible = true;
        InputCapture.Open(Time.frameCount, MouseHeld);
        _activeTabId = tabId;
        _lastClickedEntryId = null;
        _nextRefreshUtc = DateTime.UtcNow;
        RefreshTabs();
        if (!ForgeWorkshopInputShield.Show(SafeLog)) { Close(); return; }
        ForgeWorkshopInputShield.ResetOpeningDrag();
        SafeLog($"[NicokoboForge/Workshop] opened; tab={tabId}");
    }

    private static void Close()
    {
        _visible = false;
        _activeSnapshot = null;
        _lastClickedEntryId = null;
        InputCapture.Close(Time.frameCount, MouseHeld);
        if (!BlocksNativeInput)
            ForgeWorkshopInputShield.Hide();
        if (!_cursorCaptured) return;
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        _cursorCaptured = false;
    }

    private static string? SelectedId()
    {
        if (_activeSnapshot == null || _activeTabId == null) return null;
        if (Selections.TryGetValue(_activeTabId, out var selected) &&
            _activeSnapshot.Entries.Any(x => x.Id == selected)) return selected;
        selected = _activeSnapshot.Entries[0].Id;
        Selections[_activeTabId] = selected;
        return selected;
    }

    private static void RefreshTabs()
    {
        Available.Clear();
        Headers.Clear();
        _activeSnapshot = null;
        var snapshots = new Dictionary<string, ForgeWorkshopSnapshot>(
            StringComparer.Ordinal);
        foreach (var tab in Tabs.Values.OrderBy(x => x.Header.Id == _defaultChainId ? 0 : 1).ThenBy(x => x.Header.Id,
                     StringComparer.Ordinal))
        {
            ForgeWorkshopSnapshot? snapshot;
            try { snapshot = tab.Snapshot(); }
            catch (Exception ex)
            {
                SafeLog($"[ERROR] [NicokoboForge/Workshop] snapshot failed; " +
                    $"tab={tab.Header.Id}; {ex.GetType().Name}: {ex.Message}");
                continue;
            }
            if (snapshot == null || !Valid(snapshot)) continue;
            Available.Add(tab);
            Headers.Add(tab.Header);
            snapshots.Add(tab.Header.Id, snapshot);
        }
        if (Available.Count == 0) return;

        // Resolve same-chain and cross-chain prerequisites from one validated
        // refresh. A missing or duplicate ID cannot satisfy a dependency.
        var uniqueEntries = snapshots.Values.SelectMany(x => x.Entries)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        foreach (var tab in Available)
        {
            var snapshot = snapshots[tab.Header.Id];
            snapshots[tab.Header.Id] = snapshot with
            {
                Entries = snapshot.Entries.Select(entry => entry with
                {
                    PrerequisiteMet = entry.PrerequisiteMet &&
                        entry.Dependencies.All(id =>
                            uniqueEntries.TryGetValue(id, out var dependency) &&
                            dependency.Unlocked)
                }).ToArray()
            };
        }
        if (_activeTabId == null || !snapshots.TryGetValue(_activeTabId,
                out _activeSnapshot))
        {
            _activeTabId = Available[0].Header.Id;
            _activeSnapshot = snapshots[_activeTabId];
        }
    }

    private static bool Valid(ForgeWorkshopSnapshot snapshot)
    {
        if (snapshot.Entries is not { Count: > 0 and <= ForgeNumbers.Workshop.MaxEntries } ||
            snapshot.Groups is not { Count: > 0 and <= ForgeNumbers.Workshop.MaxGroups } ||
            snapshot.Groups.Any(x => x == null || x.EntryCount is < 1 or > ForgeNumbers.Workshop.MaxEntriesPerGroup ||
                string.IsNullOrWhiteSpace(x.ChineseTitle) ||
                string.IsNullOrWhiteSpace(x.EnglishTitle)) ||
            snapshot.Groups.Sum(x => x.EntryCount) != snapshot.Entries.Count ||
            snapshot.Entries.Any(x => x == null || string.IsNullOrWhiteSpace(x.Id) ||
                string.IsNullOrWhiteSpace(x.Title) || x.ActionEnabled && x.Unlocked ||
                x.Dependencies == null || x.Conditions == null || x.Rewards == null || x.VisualLinks == null ||
                x.VisualLinks.Any(string.IsNullOrWhiteSpace) || x.VisualLinks.Contains(x.Id, StringComparer.Ordinal) ||
                x.Dependencies.Any(string.IsNullOrWhiteSpace) ||
                x.Dependencies.Contains(x.Id, StringComparer.Ordinal) ||
                x.Conditions.Any(y => y == null || !ValidResource(y.Kind,
                    y.ResourceId, y.Quantity)) ||
                x.Rewards.Any(y => y == null || !ValidResource(y.Kind,
                    y.ResourceId, y.Quantity)) ||
                x.GraphX.HasValue != x.GraphY.HasValue ||
                x.GraphX.HasValue && !float.IsFinite(x.GraphX.Value) ||
                x.GraphY.HasValue && !float.IsFinite(x.GraphY.Value) ||
                x.GraphX is < -1f or > 1f || x.GraphY is < -1f or > 1f) ||
            snapshot.Entries.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count()
                != snapshot.Entries.Count)
            return false;
        return true;
    }

    public static bool OpenChain(string chainId) => OpenTab(chainId);

    private static bool ValidResource(ForgeWorkshopResourceKind kind,
        string id, int quantity) => quantity > 0 && (kind switch
        {
            ForgeWorkshopResourceKind.Credits => string.IsNullOrEmpty(id),
            ForgeWorkshopResourceKind.Item => !string.IsNullOrWhiteSpace(id),
            _ => false
        });

    internal static bool CanUnlock(ForgeWorkshopEntry entry) =>
        entry.ActionEnabled && entry.Completed != false && !entry.Unlocked && entry.PrerequisiteMet &&
        entry.Conditions.All(x => x.Satisfied);

    private static void SafeLog(string line)
    {
        try { _log?.Invoke(line); }
        catch { }
    }
}
