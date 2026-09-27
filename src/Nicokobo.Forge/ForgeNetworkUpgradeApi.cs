using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Registration;
using System.Reflection;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Nicokobo.Forge;

/// <summary>
/// Registers non-repeatable Wilds Network upgrades, persists their native state in
/// PlayerStore.networkUpgrade, and exposes their unlocked state to content Mods.
/// </summary>
public static class ForgeNetworkUpgradeApi
{
    private sealed record HeldUnlockAction(Action Managed,
        Il2CppSystem.Action Native);

    private sealed class UnlockClickState
    {
        internal NetworkUpgradeDeclaration Definition { get; init; } = null!;
        internal NetworkUpgrade Upgrade { get; init; } = null!;
        internal PlayerStore Store { get; init; } = null!;
        internal int StateBefore { get; init; }
        internal int CashBefore { get; init; }
        internal int FavorBefore { get; init; }
        internal int CreditCost { get; init; }
        internal string RunId { get; init; } = "";
        internal int SlotId { get; init; }
        internal bool CallbackRequested { get; set; }
    }

    private static readonly object Gate = new();
    private static readonly NetworkUpgradeCatalog Catalog = new();
    private static readonly Dictionary<string, HeldUnlockAction> UnlockActions =
        new(StringComparer.Ordinal);
    private static readonly HashSet<string> Conflicted = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ActionConflicts = new(StringComparer.Ordinal);
    private static Action<string>? _log;
    private static bool _enabled;
    private static IntPtr _conflictStorePointer;
    private static string? _conflictRunId;
    private static int _conflictSlotId = -1;
    [ThreadStatic] private static UnlockClickState? _unlockClick;

    public static SubmitResult Register(string ownerId, string upgradeId,
        NetworkUpgradeOptions options)
    {
        SubmitResult result;
        lock (Gate)
            result = Catalog.Submit(ownerId, upgradeId, options);
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(prefix + $"[NicokoboForge/Network] owner={ownerId}; id={upgradeId}; " +
            $"status={result.Status}; reason={result.Reason}");
        if (_enabled && result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)
            EnsureCurrentStore("late-registration");
        return result;
    }

    /// <summary>Returns true only when this process owns the ID and the current
    /// PlayerStore contains it in native unlocked state 2.</summary>
    public static bool IsUnlocked(string upgradeId)
    {
        if (!_enabled) return false;
        try
        {
            var store = PlayerStore.Instance;
            if (store == null || store.Pointer == IntPtr.Zero) return false;
            RefreshConflictScope(store);
            NetworkUpgradeDeclaration definition;
            lock (Gate)
                if (Conflicted.Contains(upgradeId) ||
                    !Catalog.TryGet(upgradeId, out definition!))
                    return false;
            var upgrades = store.networkUpgrade;
            return upgrades != null &&
                upgrades.TryGetValue(upgradeId, out var upgrade) && upgrade != null &&
                upgrade.Pointer != IntPtr.Zero &&
                upgrade.id == upgradeId &&
                upgrade.subtitle == definition.Options.EnglishSubtitle &&
                upgrade.state == 2;
        }
        catch (Exception ex)
        {
            SafeLog($"[WARN] [NicokoboForge/Network] id={upgradeId}; status=ReadFailed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static int StagedCount
    {
        get { lock (Gate) return Catalog.Snapshot().Count; }
    }

    internal static bool HooksInstalled => _enabled;

    internal static void SetLogger(Action<string> log)
    {
        NetworkUpgradeDeclaration[] staged;
        lock (Gate)
        {
            _log = log;
            staged = Catalog.Snapshot().ToArray();
        }
        foreach (var definition in staged)
            SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=Staged");
    }

    internal static bool Install(HarmonyLib.Harmony harmony, bool allowed)
    {
        _enabled = false;
        if (!allowed)
        {
            SafeLog("[NicokoboForge/Network] hooks=disabled; build or signatures unavailable");
            return false;
        }
        var installed = new List<(MethodBase Original, MethodInfo Callback)>();
        try
        {
            PatchPostfix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.InitUpgradeDict), Type.EmptyTypes,
                nameof(InitUpgradeDictPostfix), installed);
            PatchPostfix(harmony, typeof(ModHook),
                nameof(ModHook.FireOnGameLoadedLate), Type.EmptyTypes,
                nameof(GameLoadedLatePostfix), installed);
            PatchPostfix(harmony, typeof(WildUIManager),
                nameof(WildUIManager.OpenUI), [typeof(bool)],
                nameof(WildUiOpenPostfix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetLocalizedTitle), Type.EmptyTypes,
                nameof(LocalizedTitlePrefix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetLocalizedSubtitle), Type.EmptyTypes,
                nameof(LocalizedSubtitlePrefix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetLocalizedDescription), Type.EmptyTypes,
                nameof(LocalizedDescriptionPrefix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetLocalizedAlreadyBought), Type.EmptyTypes,
                nameof(LocalizedAlreadyBoughtPrefix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetCost), Type.EmptyTypes,
                nameof(GetCostPrefix), installed);
            PatchPostfix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.IsReady), Type.EmptyTypes,
                nameof(IsReadyPostfix), installed);
            PatchPostfix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.GetMissingPrerequisite), Type.EmptyTypes,
                nameof(GetMissingPrerequisitePostfix), installed);
            PatchPrefix(harmony, typeof(NetworkUpgrade),
                nameof(NetworkUpgrade.Unlock), Type.EmptyTypes,
                nameof(UnlockPrefix), installed);
            var unlockClicked = AccessTools.Method(typeof(WildUIManager),
                nameof(WildUIManager.OnUnlockClicked), Type.EmptyTypes)
                ?? throw new MissingMethodException(nameof(WildUIManager),
                    nameof(WildUIManager.OnUnlockClicked));
            var clickPrefix = AccessTools.Method(typeof(ForgeNetworkUpgradeApi),
                nameof(UnlockClickedPrefix))!;
            var clickPostfix = AccessTools.Method(typeof(ForgeNetworkUpgradeApi),
                nameof(UnlockClickedPostfix))!;
            var clickFinalizer = AccessTools.Method(typeof(ForgeNetworkUpgradeApi),
                nameof(UnlockClickedFinalizer))!;
            installed.Add((unlockClicked, clickPrefix));
            installed.Add((unlockClicked, clickPostfix));
            installed.Add((unlockClicked, clickFinalizer));
            harmony.Patch(unlockClicked,
                prefix: new HarmonyMethod(clickPrefix),
                postfix: new HarmonyMethod(clickPostfix),
                finalizer: new HarmonyMethod(clickFinalizer));
            _enabled = true;
            SafeLog("[NicokoboForge/Network] hooks=installed; buildGated=true");
            return true;
        }
        catch (Exception ex)
        {
            _enabled = false;
            _unlockClick = null;
            for (int index = installed.Count - 1; index >= 0; index--)
                try { harmony.Unpatch(installed[index].Original, installed[index].Callback); }
                catch (Exception unpatchError)
                {
                    SafeLog($"[ERROR] [NicokoboForge/Network] hook cleanup failed; " +
                        $"callback={installed[index].Callback.Name}; " +
                        $"reason={unpatchError.GetType().Name}: {unpatchError.Message}");
                }
            SafeLog($"[ERROR] [NicokoboForge/Network] hooks=disabled; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void PatchPostfix(HarmonyLib.Harmony harmony, Type type,
        string methodName, Type[] parameters, string callbackName,
        List<(MethodBase Original, MethodInfo Callback)> installed)
    {
        var original = AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.Name, methodName);
        var callback = AccessTools.Method(typeof(ForgeNetworkUpgradeApi), callbackName)
            ?? throw new MissingMethodException(nameof(ForgeNetworkUpgradeApi), callbackName);
        installed.Add((original, callback));
        harmony.Patch(original, postfix: new HarmonyMethod(callback));
    }

    private static void PatchPrefix(HarmonyLib.Harmony harmony, Type type,
        string methodName, Type[] parameters, string callbackName,
        List<(MethodBase Original, MethodInfo Callback)> installed)
    {
        var original = AccessTools.Method(type, methodName, parameters)
            ?? throw new MissingMethodException(type.Name, methodName);
        var callback = AccessTools.Method(typeof(ForgeNetworkUpgradeApi), callbackName)
            ?? throw new MissingMethodException(nameof(ForgeNetworkUpgradeApi), callbackName);
        installed.Add((original, callback));
        harmony.Patch(original, prefix: new HarmonyMethod(callback));
    }

    private static NetworkUpgradeDeclaration[] Snapshot()
    {
        lock (Gate) return Catalog.Snapshot().ToArray();
    }

    private static void InitUpgradeDictPostfix(
        ref Il2CppSystem.Collections.Generic.Dictionary<string, NetworkUpgrade> __result)
    {
        if (!_enabled) return;
        try { ApplyToDictionary(__result, "native-init"); }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] status=StoreApplyFailed; source=native-init; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void GameLoadedLatePostfix() =>
        EnsureCurrentStore("game-loaded-late");

    private static void WildUiOpenPostfix(WildUIManager __instance, bool debug) // debug is native API shape
    {
        EnsureCurrentStore("ui-open");
        EnsureUi(__instance, "ui-open");
    }

    private static void EnsureCurrentStore(string source)
    {
        if (!_enabled) return;
        try
        {
            var store = PlayerStore.Instance;
            if (store == null || store.Pointer == IntPtr.Zero || store.networkUpgrade == null)
                return;
            RefreshConflictScope(store);
            ApplyToDictionary(store.networkUpgrade, source);
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] status=StoreApplyFailed; source={source}; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ApplyToDictionary(
        Il2CppSystem.Collections.Generic.Dictionary<string, NetworkUpgrade>? upgrades,
        string source)
    {
        if (upgrades == null) return;
        int added = 0;
        foreach (var definition in Snapshot())
        {
            try
            {
                if (IsConflicted(definition.UpgradeId)) continue;
                if (upgrades.TryGetValue(definition.UpgradeId, out var existing))
                {
                    // A loaded Forge entry carries the subtitle staged when it was
                    // created. Do not rewrite an unrelated Mod's same-ID entry.
                    if (existing == null || existing.Pointer == IntPtr.Zero ||
                        existing.id != definition.UpgradeId ||
                        existing.subtitle != definition.Options.EnglishSubtitle)
                    {
                        MarkConflict(definition,
                            "native dictionary entry is not a Forge definition");
                        continue;
                    }
                    if (!EnsureUnlockAction(definition.UpgradeId)) continue;
                    ApplyDefinition(existing, definition, preserveState: true);
                    continue;
                }
                if (!EnsureUnlockAction(definition.UpgradeId)) continue;
                var upgrade = new NetworkUpgrade(definition.UpgradeId);
                ApplyDefinition(upgrade, definition, preserveState: false);
                upgrades.Add(definition.UpgradeId, upgrade);
                added++;
            }
            catch (Exception ex)
            {
                SafeLog($"[WARN] [NicokoboForge/Network] owner={definition.OwnerId}; " +
                    $"id={definition.UpgradeId}; status=ApplyDeferred; source={source}; " +
                    $"reason={ex.GetType().Name}: {ex.Message}");
            }
        }
        if (added != 0)
        {
            SafeLog($"[NicokoboForge/Network] status=Applied; source={source}; " +
                $"added={added}");
        }
    }

    private static void ApplyDefinition(NetworkUpgrade upgrade,
        NetworkUpgradeDeclaration definition, bool preserveState)
    {
        int state = preserveState && upgrade.state is 1 or 2 ? upgrade.state : 1;
        upgrade.id = definition.UpgradeId;
        upgrade.subtitle = definition.Options.EnglishSubtitle;
        upgrade.cost = ResolveCost(definition);
        upgrade.isRepeatable = false;
        upgrade.cooldownCurrent = 0;
        upgrade.cooldownDuration = 0;
        upgrade.lockInDemo = false;
        upgrade.state = state;
        var prerequisites = upgrade.prerequisite;
        if (prerequisites == null)
            throw new InvalidOperationException("Network upgrade prerequisite list is unavailable");
        prerequisites.Clear();
        foreach (string prerequisite in definition.Prerequisites)
            prerequisites.Add(prerequisite);
    }

    private static bool EnsureUnlockAction(string upgradeId)
    {
        NetworkUpgradeDeclaration definition;
        HeldUnlockAction held;
        lock (Gate)
        {
            if (Conflicted.Contains(upgradeId) ||
                !Catalog.TryGet(upgradeId, out definition!)) return false;
            if (!UnlockActions.TryGetValue(upgradeId, out held!))
            {
                Action managed = () => InvokeUnlocked(upgradeId);
                var native = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(managed)
                    ?? throw new InvalidOperationException("Native unlock delegate conversion failed");
                held = new HeldUnlockAction(managed, native);
                UnlockActions.Add(upgradeId, held);
            }
        }
        var actions = NetworkUpgradeList.UnlockActions;
        if (actions == null)
            throw new InvalidOperationException("Native network unlock action registry is unavailable");
        if (actions.TryGetValue(upgradeId, out var existing))
        {
            if (existing == null || existing.Pointer != held.Native.Pointer)
            {
                MarkConflict(definition,
                    "native unlock action belongs to another owner", action: true);
                return false;
            }
            return true;
        }
        actions.Add(upgradeId, held.Native);
        return true;
    }

    private static bool IsConflicted(string upgradeId)
    {
        lock (Gate) return Conflicted.Contains(upgradeId);
    }

    private static void MarkConflict(NetworkUpgradeDeclaration definition,
        string reason, bool action = false)
    {
        bool first;
        lock (Gate)
        {
            first = Conflicted.Add(definition.UpgradeId);
            if (action) ActionConflicts.Add(definition.UpgradeId);
        }
        if (first)
            SafeLog($"[WARN] [NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=Conflict; reason={reason}");
    }

    private static void RefreshConflictScope(PlayerStore store)
    {
        string runId = store.runID;
        int slotId = store.saveSlotId;
        lock (Gate)
        {
            if (_conflictStorePointer == store.Pointer &&
                _conflictRunId == runId && _conflictSlotId == slotId) return;
            Conflicted.RemoveWhere(id => !ActionConflicts.Contains(id));
            _conflictStorePointer = store.Pointer;
            _conflictRunId = runId;
            _conflictSlotId = slotId;
        }
    }

    private static void InvokeUnlocked(string upgradeId)
    {
        if (!_enabled) return;
        NetworkUpgradeDeclaration definition;
        lock (Gate)
            if (!Catalog.TryGet(upgradeId, out definition!)) return;
        try
        {
            var store = PlayerStore.Instance;
            var upgrades = store?.networkUpgrade;
            if (store == null || store.Pointer == IntPtr.Zero || upgrades == null ||
                !upgrades.TryGetValue(upgradeId, out var upgrade) ||
                upgrade == null || upgrade.Pointer == IntPtr.Zero)
                return;
            RefreshConflictScope(store);
            if (IsConflicted(upgradeId)) return;
            if (upgrade.id != upgradeId ||
                upgrade.subtitle != definition.Options.EnglishSubtitle)
            {
                MarkConflict(definition,
                    "unlock callback target is not a Forge definition");
                return;
            }
            if (_unlockClick is { } click &&
                click.Definition.UpgradeId == upgradeId)
            {
                click.CallbackRequested = true;
                return;
            }
            definition.Options.OnUnlocked?.Invoke();
            SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; id={upgradeId}; " +
                "status=Unlocked");
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] owner={definition.OwnerId}; id={upgradeId}; " +
                $"status=UnlockCallbackFailed; reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void EnsureUi(WildUIManager? manager, string source)
    {
        if (!_enabled || manager == null || manager.Pointer == IntPtr.Zero ||
            manager.networkElements == null) return;
        try
        {
            var store = PlayerStore.Instance;
            var upgrades = store?.networkUpgrade;
            if (store == null || store.Pointer == IntPtr.Zero || upgrades == null)
                return;
            RefreshConflictScope(store);
            SyncHierarchyElements(manager, source);
            var pending = new List<NetworkUpgradeDeclaration>();
            foreach (var definition in Snapshot())
            {
                if (IsConflicted(definition.UpgradeId))
                {
                    HideOwnedElement(manager, definition.UpgradeId);
                    continue;
                }
                if (!upgrades.TryGetValue(definition.UpgradeId, out var applied) ||
                    applied == null || applied.Pointer == IntPtr.Zero)
                {
                    HideOwnedElement(manager, definition.UpgradeId);
                    continue;
                }
                if (applied.id != definition.UpgradeId ||
                    applied.subtitle != definition.Options.EnglishSubtitle)
                {
                    MarkConflict(definition,
                        "current native upgrade is not a Forge definition");
                    HideOwnedElement(manager, definition.UpgradeId);
                    continue;
                }
                if (HasForeignElement(manager, definition.UpgradeId))
                {
                    MarkConflict(definition,
                        "native UI element belongs to another owner");
                    HideOwnedElement(manager, definition.UpgradeId);
                    continue;
                }
                bool visible = IsVisible(definition);
                NetworkElement? element = FindElement(manager, definition.UpgradeId);
                if (element != null)
                {
                    if (element.gameObject.name != OwnedElementName(definition.UpgradeId))
                    {
                        MarkConflict(definition,
                            "native UI element belongs to another owner");
                        continue;
                    }
                    element.gameObject.SetActive(visible);
                    if (visible) element.UpdateElement();
                    continue;
                }
                pending.Add(definition);
            }
            for (int pass = 0; pass < pending.Count && pending.Count != 0; pass++)
            {
                bool progressed = false;
                foreach (var definition in pending.ToArray())
                {
                    var anchor = FindElement(manager, definition.Options.UiAnchorId);
                    if (anchor == null) continue;
                    CreateUiElement(manager, definition, anchor, source);
                    pending.Remove(definition);
                    progressed = true;
                }
                if (!progressed) break;
            }
            foreach (var definition in pending)
            {
                var fallback = FirstElement(manager);
                if (fallback == null)
                {
                    SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                        $"id={definition.UpgradeId}; status=UiDeferred; source={source}; " +
                        "reason=no native anchor element");
                    continue;
                }
                CreateUiElement(manager, definition, fallback, source + "-fallback");
            }
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] status=UiFailed; source={source}; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SyncHierarchyElements(WildUIManager manager, string source)
    {
        var hierarchy = manager.GetComponentsInChildren<NetworkElement>(true);
        if (hierarchy == null || hierarchy.Length == 0) return;
        int added = 0;
        for (int hierarchyIndex = 0; hierarchyIndex < hierarchy.Length;
             hierarchyIndex++)
        {
            var candidate = hierarchy[hierarchyIndex];
            if (candidate == null || candidate.Pointer == IntPtr.Zero ||
                string.IsNullOrWhiteSpace(candidate.id)) continue;
            bool present = false;
            for (int listIndex = 0; listIndex < manager.networkElements.Count;
                 listIndex++)
            {
                var existing = manager.networkElements[listIndex];
                if (existing != null && existing.Pointer != IntPtr.Zero &&
                    (existing.Pointer == candidate.Pointer || existing.id == candidate.id))
                {
                    present = true;
                    break;
                }
            }
            if (present) continue;
            manager.networkElements.Add(candidate);
            added++;
        }
        if (added != 0)
            SafeLog($"[NicokoboForge/Network] status=UiAnchorsDiscovered; " +
                $"source={source}; added={added}; total={manager.networkElements.Count}");
    }

    private static void CreateUiElement(WildUIManager manager,
        NetworkUpgradeDeclaration definition, NetworkElement anchor, string source)
    {
        if (anchor.gameObject == null)
            throw new InvalidOperationException("Network anchor object is unavailable");
        var cloneObject = UnityEngine.Object.Instantiate(anchor.gameObject,
            anchor.transform.parent);
        cloneObject.name = OwnedElementName(definition.UpgradeId);
        var element = cloneObject.GetComponent<NetworkElement>();
        if (element == null || element.Pointer == IntPtr.Zero)
        {
            UnityEngine.Object.Destroy(cloneObject);
            throw new InvalidOperationException("Cloned network element is unavailable");
        }
        element.id = definition.UpgradeId;
        element.transform.localPosition = anchor.transform.localPosition +
            new Vector3(definition.Options.UiOffsetX,
                definition.Options.UiOffsetY, 0f);
        manager.networkElements.Add(element);
        bool visible = IsVisible(definition);
        element.gameObject.SetActive(visible);
        if (visible) element.UpdateElement();
        SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
            $"id={definition.UpgradeId}; status=UiApplied; source={source}; " +
            $"anchor={definition.Options.UiAnchorId}; " +
            $"offset={definition.Options.UiOffsetX},{definition.Options.UiOffsetY}");
    }

    private static NetworkElement? FindElement(WildUIManager manager, string id)
    {
        for (int index = 0; index < manager.networkElements.Count; index++)
        {
            var element = manager.networkElements[index];
            if (element != null && element.Pointer != IntPtr.Zero && element.id == id)
                return element;
        }
        return null;
    }

    private static string OwnedElementName(string upgradeId) =>
        $"NicokoboForge.Network.{upgradeId}";

    private static bool HasForeignElement(WildUIManager manager, string upgradeId)
    {
        var hierarchy = manager.GetComponentsInChildren<NetworkElement>(true);
        for (int index = 0; index < hierarchy.Length; index++)
        {
            var element = hierarchy[index];
            if (element != null && element.Pointer != IntPtr.Zero &&
                element.id == upgradeId && element.gameObject != null &&
                element.gameObject.name != OwnedElementName(upgradeId))
                return true;
        }
        for (int index = 0; index < manager.networkElements.Count; index++)
        {
            var element = manager.networkElements[index];
            if (element != null && element.Pointer != IntPtr.Zero &&
                element.id == upgradeId && element.gameObject != null &&
                element.gameObject.name != OwnedElementName(upgradeId))
                return true;
        }
        return false;
    }

    private static void HideOwnedElement(WildUIManager manager, string upgradeId)
    {
        for (int index = 0; index < manager.networkElements.Count; index++)
        {
            var element = manager.networkElements[index];
            if (element != null && element.Pointer != IntPtr.Zero &&
                element.id == upgradeId && element.gameObject != null &&
                element.gameObject.name == OwnedElementName(upgradeId))
                element.gameObject.SetActive(false);
        }
    }

    private static NetworkElement? FirstElement(WildUIManager manager)
    {
        for (int index = 0; index < manager.networkElements.Count; index++)
        {
            var element = manager.networkElements[index];
            if (element != null && element.Pointer != IntPtr.Zero) return element;
        }
        return null;
    }

    private static bool IsVisible(NetworkUpgradeDeclaration definition)
    {
        try { return definition.Options.IsVisible?.Invoke() ?? true; }
        catch (Exception ex)
        {
            SafeLog($"[WARN] [NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=VisibilityFailed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool LocalizedTitlePrefix(NetworkUpgrade __instance,
        ref string __result) => LocalizedPrefix(__instance, ref __result,
            x => PreferEnglish() ? x.Options.EnglishTitle : x.Options.ChineseTitle);

    private static bool LocalizedSubtitlePrefix(NetworkUpgrade __instance,
        ref string __result) => LocalizedPrefix(__instance, ref __result,
            x => PreferEnglish() ? x.Options.EnglishSubtitle : x.Options.ChineseSubtitle);

    private static bool LocalizedDescriptionPrefix(NetworkUpgrade __instance,
        ref string __result) => LocalizedPrefix(__instance, ref __result,
            x => PreferEnglish() ? x.Options.EnglishDescription : x.Options.ChineseDescription);

    private static bool LocalizedAlreadyBoughtPrefix(NetworkUpgrade __instance,
        ref string __result) => LocalizedPrefix(__instance, ref __result,
            x => PreferEnglish() ? x.Options.EnglishAlreadyBought : x.Options.ChineseAlreadyBought);

    private static bool GetCostPrefix(NetworkUpgrade __instance, ref int __result)
    {
        if (!TryGetDefinition(__instance, out var definition)) return true;
        __result = ResolveCost(definition);
        return false;
    }

    private static void IsReadyPostfix(NetworkUpgrade __instance, ref bool __result)
    {
        if (!_enabled || !__result || !TryGetDefinition(__instance, out var definition))
            return;
        var store = PlayerStore.Instance;
        if (store == null || store.Pointer == IntPtr.Zero ||
            store.playerCash < ResolveCreditCost(definition))
        {
            __result = false;
            return;
        }
        if (definition.Options.GetRequirement != null)
            __result = ResolveRequirement(definition).Satisfied;
    }

    private static void GetMissingPrerequisitePostfix(NetworkUpgrade __instance,
        ref string __result)
    {
        if (!_enabled || !TryGetDefinition(__instance, out var definition)) return;
        var store = PlayerStore.Instance;
        int creditCost = ResolveCreditCost(definition);
        if (creditCost > 0 && (store == null || store.Pointer == IntPtr.Zero ||
                store.playerCash < creditCost))
            AppendMissing(ref __result, PreferEnglish()
                ? $"Requires {creditCost:N0} credits."
                : $"需要 {creditCost:N0} 信用点。");
        if (definition.Options.GetRequirement != null)
        {
            var requirement = ResolveRequirement(definition);
            if (!requirement.Satisfied)
                AppendMissing(ref __result, PreferEnglish()
                    ? requirement.EnglishMissingText : requirement.ChineseMissingText);
        }
    }

    private static bool UnlockPrefix(NetworkUpgrade __instance)
    {
        if (!_enabled) return true;
        if (!TryGetDefinition(__instance, out var definition) ||
            definition.Options.GetRequirement == null && ResolveCreditCost(definition) == 0)
            return true;
        bool authorized = _unlockClick?.Definition.UpgradeId == definition.UpgradeId;
        if (!authorized)
            SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=UnlockRejected; " +
                "reason=material-gated upgrades must use the native purchase flow");
        return authorized;
    }

    private static bool UnlockClickedPrefix(WildUIManager __instance)
    {
        if (!_enabled) return true;
        _unlockClick = null;
        try
        {
            if (__instance == null || __instance.Pointer == IntPtr.Zero ||
                string.IsNullOrWhiteSpace(__instance.currentSelectedId)) return true;
            NetworkUpgradeDeclaration definition;
            lock (Gate)
                if (!Catalog.TryGet(__instance.currentSelectedId, out definition!))
                    return true;
            var store = PlayerStore.Instance;
            if (store != null && store.Pointer != IntPtr.Zero)
                RefreshConflictScope(store);
            var upgrades = store?.networkUpgrade;
            if (store == null || store.Pointer == IntPtr.Zero || upgrades == null ||
                !upgrades.TryGetValue(definition.UpgradeId, out var upgrade) ||
                upgrade == null || upgrade.Pointer == IntPtr.Zero) return false;
            bool ownedUpgrade = upgrade.id == definition.UpgradeId &&
                upgrade.subtitle == definition.Options.EnglishSubtitle;
            if (IsConflicted(definition.UpgradeId)) return !ownedUpgrade;
            if (!ownedUpgrade)
            {
                MarkConflict(definition,
                    "purchase target is not a Forge definition");
                return true;
            }
            int creditCost = ResolveCreditCost(definition);
            if (definition.Options.GetRequirement == null && creditCost == 0) return true;
            bool creditReady = store.playerCash >= creditCost;
            bool requirementReady = definition.Options.GetRequirement == null ||
                ResolveRequirement(definition).Satisfied;
            if (!creditReady || !requirementReady)
            {
                SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                    $"id={definition.UpgradeId}; status=RequirementMissing");
                __instance.OnElementClicked(definition.UpgradeId);
                return false;
            }
            if (!Guid.TryParse(store.runID, out var runId) ||
                runId == Guid.Empty || store.saveSlotId < 0)
                return false;
            _unlockClick = new UnlockClickState
            {
                Definition = definition,
                Upgrade = upgrade,
                Store = store,
                StateBefore = upgrade.state,
                CashBefore = store.playerCash,
                FavorBefore = store.wildFavor,
                CreditCost = creditCost,
                RunId = store.runID,
                SlotId = store.saveSlotId
            };
            return true;
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] status=RequirementCheckFailed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            _unlockClick = null;
            return false;
        }
    }

    private static void UnlockClickedPostfix(WildUIManager __instance)
    {
        if (!_enabled) return;
        var click = _unlockClick;
        _unlockClick = null;
        if (click == null) return;
        try
        {
            if (!click.CallbackRequested || click.Upgrade.state != 2)
            {
                RollbackClick(click, "native unlock did not complete");
                return;
            }
            int creditCost = click.CreditCost;
            if (creditCost > 0)
            {
                if (click.Store.playerCash < creditCost)
                {
                    RollbackClick(click, "credit balance changed before consumption");
                    __instance.OnElementClicked(click.Definition.UpgradeId);
                    return;
                }
                click.Store.ModCash(-creditCost, false);
                if (click.Store.playerCash != click.CashBefore - creditCost)
                    throw new InvalidOperationException("Credit debit was not observed");
            }
            if (click.Definition.Options.TryConsumeRequirement != null &&
                !click.Definition.Options.TryConsumeRequirement.Invoke())
            {
                RollbackClick(click, "requirement consumption failed");
                __instance.OnElementClicked(click.Definition.UpgradeId);
                return;
            }
            // The native click may save before Forge's additional costs are consumed.
            // Save once more so the unlocked node, credit debit, and material change
            // share the same native persistence boundary.
            Exception? saveError = null;
            try { click.Store.SaveGame(); }
            catch (Exception ex)
            {
                saveError = ex;
                SafeLog($"[WARN] [NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                    $"id={click.Definition.UpgradeId}; status=SaveCallFailed; " +
                    $"reason={ex.GetType().Name}: {ex.Message}");
            }
            var readback = VerifySave(click, 2,
                click.CashBefore - creditCost, click.Store.wildFavor);
            bool callbackSucceeded = true;
            try { click.Definition.Options.OnUnlocked?.Invoke(); }
            catch (Exception ex)
            {
                callbackSucceeded = false;
                SafeLog($"[ERROR] [NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                    $"id={click.Definition.UpgradeId}; status=UnlockCallbackFailed; " +
                    $"reason={ex.GetType().Name}: {ex.Message}");
            }
            bool complete = readback.Status == NetworkUpgradeSaveStatus.Matched &&
                callbackSucceeded;
            SafeLog($"{(complete ? "" : "[WARN] ")}" +
                $"[NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                $"id={click.Definition.UpgradeId}; " +
                $"status={(complete ? "Unlocked" : callbackSucceeded ? "PersistenceUncertain" : "EffectUncertain")}; " +
                $"creditCost={creditCost}; requirement=Consumed; " +
                $"readback={readback.Status}; saveCall={saveError?.GetType().Name ?? "Returned"}");
        }
        catch (Exception ex)
        {
            RollbackClick(click, $"requirement consumption exception {ex.GetType().Name}");
        }
    }

    private static Exception? UnlockClickedFinalizer(Exception? __exception)
    {
        var click = _unlockClick;
        _unlockClick = null;
        if (!_enabled) return __exception;
        if (click != null)
            RollbackClick(click, __exception == null
                ? "unlock exited before completion" : $"native exception {__exception.GetType().Name}");
        return __exception;
    }

    private static void RollbackClick(UnlockClickState click, string reason)
    {
        try
        {
            var current = PlayerStore.Instance;
            var upgrades = click.Store.networkUpgrade;
            if (current == null || current.Pointer != click.Store.Pointer ||
                click.Store.runID != click.RunId ||
                click.Store.saveSlotId != click.SlotId || upgrades == null ||
                !upgrades.TryGetValue(click.Definition.UpgradeId, out var upgrade) ||
                upgrade == null || upgrade.Pointer != click.Upgrade.Pointer)
                throw new InvalidOperationException("Run or upgrade identity changed");
            click.Upgrade.state = click.StateBefore;
            click.Store.playerCash = click.CashBefore;
            click.Store.wildFavor = click.FavorBefore;
            if (click.Upgrade.state != click.StateBefore ||
                click.Store.playerCash != click.CashBefore ||
                click.Store.wildFavor != click.FavorBefore)
                throw new InvalidOperationException("Rollback was not observed in memory");
            // The native click may have saved before this postfix or finalizer.
            // Compensate on the same run and slot after restoring native values.
            Exception? saveError = null;
            try { click.Store.SaveGame(); }
            catch (Exception ex) { saveError = ex; }
            var readback = VerifySave(click, click.StateBefore,
                click.CashBefore, click.FavorBefore);
            if (readback.Status == NetworkUpgradeSaveStatus.Matched)
                SafeLog($"[WARN] [NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                    $"id={click.Definition.UpgradeId}; status=RolledBack; " +
                    $"reason={reason}; readback=Matched; " +
                    $"saveCall={saveError?.GetType().Name ?? "Returned"}");
            else
                SafeLog($"[ERROR] [NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                    $"id={click.Definition.UpgradeId}; status=PersistenceUncertain; " +
                    $"rollbackReason={reason}; readback={readback.Status}; " +
                    $"saveCall={saveError?.GetType().Name ?? "Returned"}; " +
                    $"reason={readback.Reason}");
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] owner={click.Definition.OwnerId}; " +
                $"id={click.Definition.UpgradeId}; status=PersistenceUncertain; " +
                $"rollbackReason={reason}; reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static NetworkUpgradeSaveResult VerifySave(UnlockClickState click,
        int state, int cash, int favor)
    {
        try
        {
            return NetworkUpgradeSaveReadback.Compare(Application.persistentDataPath,
                click.RunId, click.SlotId, click.Definition.UpgradeId,
                state, cash, favor);
        }
        catch (Exception ex)
        {
            return new(NetworkUpgradeSaveStatus.Unavailable,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static NetworkUpgradeRequirement ResolveRequirement(
        NetworkUpgradeDeclaration definition)
    {
        try
        {
            var value = definition.Options.GetRequirement?.Invoke();
            return value ?? new(false, "Additional requirement unavailable.", "额外解锁条件不可用。");
        }
        catch (Exception ex)
        {
            SafeLog($"[ERROR] [NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=RequirementCheckFailed; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return new(false, "Additional requirement unavailable.", "额外解锁条件不可用。");
        }
    }

    private static bool TryGetDefinition(NetworkUpgrade? upgrade,
        out NetworkUpgradeDeclaration definition)
    {
        definition = null!;
        if (!_enabled || upgrade == null || upgrade.Pointer == IntPtr.Zero ||
            string.IsNullOrWhiteSpace(upgrade.id)) return false;
        lock (Gate)
            if (Conflicted.Contains(upgrade.id) ||
                !Catalog.TryGet(upgrade.id, out definition!)) return false;
        return upgrade.id == definition.UpgradeId &&
            upgrade.subtitle == definition.Options.EnglishSubtitle;
    }

    private static int ResolveCost(NetworkUpgradeDeclaration definition)
    {
        try
        {
            int cost = definition.Options.GetCost?.Invoke() ?? definition.Options.Cost;
            if (cost >= 0) return cost;
            throw new InvalidOperationException("Dynamic network upgrade cost is negative");
        }
        catch (Exception ex)
        {
            SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=CostFallback; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return definition.Options.Cost;
        }
    }

    private static int ResolveCreditCost(NetworkUpgradeDeclaration definition)
    {
        try
        {
            int cost = definition.Options.GetCreditCost?.Invoke() ??
                definition.Options.CreditCost;
            if (cost >= 0) return cost;
            throw new InvalidOperationException(
                "Dynamic network upgrade credit cost is negative");
        }
        catch (Exception ex)
        {
            SafeLog($"[NicokoboForge/Network] owner={definition.OwnerId}; " +
                $"id={definition.UpgradeId}; status=CreditCostFallback; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return definition.Options.CreditCost;
        }
    }

    private static void AppendMissing(ref string result, string missing)
    {
        if (!string.IsNullOrWhiteSpace(missing))
            result = string.IsNullOrWhiteSpace(result)
                ? missing : $"{result}\n{missing}";
    }

    private static bool LocalizedPrefix(NetworkUpgrade? upgrade, ref string result,
        Func<NetworkUpgradeDeclaration, string> select)
    {
        if (!TryGetDefinition(upgrade, out var definition)) return true;
        result = select(definition);
        return false;
    }

    private static bool PreferEnglish()
    {
        try
        {
            string? code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            return !string.IsNullOrWhiteSpace(code) &&
                code.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static void SafeLog(string message)
    {
        try { _log?.Invoke(message); }
        catch { /* Logging must not alter unlock state. */ }
    }
}
