using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nicokobo.Forge;

public sealed record ForgeModuleActionContext(GameItem Module, GameInventory Inventory, GameItem Machine);

/// <summary>Module bay contributions and owned module actions. Admission adds
/// types to the existing native list and always runs the original setup.</summary>
public static partial class ForgeModuleApi
{
    private static readonly ModuleAdmissionCatalog Admission = new();
    private static readonly OwnedCallbacks<string, ForgeModuleActionContext> Actions = new();
    private static bool _allowed, _admissionInstalled, _actionInstalled;
    private static bool _actionLocalizationReady, _actionFailed;
    private static Action<string>? _log;
    internal static void Configure(bool allowed, Action<string> log) { _allowed = allowed; _log = log; }
    public static SubmitResult RegisterAdmission(string ownerId, IReadOnlyList<ForgeModuleAdmission> rules)
    {
        if (!_allowed) return new(SubmitStatus.Invalid, "Module bay adapter is unavailable");
        return Admission.Submit(ownerId, rules, EnsureAdmission);
    }
    public static void RemoveAdmission(string ownerId)
    {
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, ownerId + ".admission")) throw new ArgumentException("Invalid owner ID");
        if (Admission.RemoveOwner(ownerId) && _admissionInstalled)
        {
            _admissionInstalled = false;
            NativeHookSet.Remove("nicokobo.forge.module_admission", _log);
        }
    }
    /// <summary>Retains the callback immediately. The native action hook waits
    /// for the game's existing localization initialization to finish.</summary>
    public static IDisposable SubscribeAction(string ownerId, string callbackId, string moduleId,
        Action<ForgeModuleActionContext> callback)
    {
        if (!OwnedCallbacks<string, ForgeModuleActionContext>.ValidId(ownerId, moduleId))
            throw new ArgumentException("Module ID must belong to its owner");
        if (!_allowed || _actionFailed)
            throw new InvalidOperationException("Module action adapter is unavailable");
        var lease = Actions.Add(ownerId, callbackId, moduleId, callback);
        if (_actionLocalizationReady && !EnsureActions())
        {
            _actionFailed = true;
            lease.Dispose();
            throw new InvalidOperationException("Module action adapter is unavailable");
        }
        return new CallbackLease(() =>
        {
            lease.Dispose();
            if (Actions.Count != 0 || !_actionInstalled) return;
            _actionInstalled = false;
            NativeHookSet.Remove("nicokobo.forge.module_actions", _log);
        });
    }
    internal static void UpdateActions()
    {
        if (!_allowed || _actionFailed || _actionInstalled || Actions.Count == 0) return;
        try
        {
            if (!_actionLocalizationReady)
            {
                if (!LocalizationSettings.HasSettings) return;
                // Resolving ModuleEffectHelper's generated metadata also runs
                // its native static constructor, which reads localized text.
                // Inspect the existing handle without starting or blocking it.
                var operation = LocalizationSettings.Instance.m_InitializingOperationHandle;
                if (operation == null || !operation.IsValid() || !operation.IsDone) return;
                if (operation.Status != AsyncOperationStatus.Succeeded)
                {
                    _actionFailed = true;
                    LogAction("[WARN] [NicokoboForge/Modules] actions disabled: native localization initialization failed");
                    return;
                }
                _actionLocalizationReady = true;
            }
            _actionFailed = !EnsureActions();
            if (_actionInstalled)
                LogAction($"[NicokoboForge/Modules] actionHook=installed; subscribers={Actions.Count}");
        }
        catch (Exception ex)
        {
            _actionFailed = true;
            LogAction($"[WARN] [NicokoboForge/Modules] actions disabled: {ex.GetType().Name}: {ex.Message}");
        }
    }
    private static void LogAction(string message) { try { _log?.Invoke(message); } catch { } }
    public static GameGridInventory? GetInventory(GameItem machine)
    {
        if (!_allowed || machine == null || machine.Pointer == IntPtr.Zero) return null;
        return NativeModuleInventory.Read(machine);
    }
    public static IReadOnlyList<GameItem> FindInstalled(GameItem machine, string moduleId, string? moduleType = null)
    {
        var items = GetInventory(machine)?.items;
        var result = new List<GameItem>();
        if (items != null) for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && item.Pointer != IntPtr.Zero && item.identifier == moduleId &&
                (moduleType == null || item.GetTagReadonly("MODULE_TYPE")?.valueString == moduleType)) result.Add(item);
        }
        return result.AsReadOnly();
    }
    public static void Recompute(GameItem machine)
    {
        var grid = GetInventory(machine) ?? throw new InvalidOperationException("Module inventory unavailable");
        ModuleEffectHelper.ComputeAllModuleTempStat(grid.items);
        ModuleHelper.ComputeModuleEffect(grid, null, machine);
        ModuleHelper.ApplyBasicModuleEffect(grid, null, machine);
    }
    private static bool EnsureAdmission() => _admissionInstalled || (_admissionInstalled = NativeHookSet.Install(
        "nicokobo.forge.module_admission", [new(typeof(MachineHelper), nameof(MachineHelper.SetupModuleBay),
            [typeof(GameInventory), typeof(GameItem), typeof(Il2CppSystem.Action), typeof(Il2CppStringArray)],
            typeof(void), typeof(ForgeModuleApi), nameof(BeforeSetup))], _log));
    private static bool EnsureActions() => _actionInstalled || (_actionInstalled = NativeHookSet.Install(
        "nicokobo.forge.module_actions", [new(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ActivateOnUseAction),
            [typeof(GameItem), typeof(GameInventory), typeof(GameItem)], typeof(void), typeof(ForgeModuleApi), nameof(BeforeAction))], _log));
    private static void BeforeSetup(GameItem machine, ref Il2CppStringArray allowedModuleTypes)
    {
        if (!_admissionInstalled || machine == null || machine.Pointer == IntPtr.Zero) return;
        try
        {
            string[] original = allowedModuleTypes?.ToArray() ?? [];
            var merged = Admission.Merge(machine.identifier, original);
            if (allowedModuleTypes == null && merged.Length != 0)
                merged = new[] { "MODULE_TYPE_UNIVERSAL" }.Concat(merged).Distinct(StringComparer.Ordinal).ToArray();
            if (!original.SequenceEqual(merged, StringComparer.Ordinal)) allowedModuleTypes = new Il2CppStringArray(merged);
        }
        catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Modules] admission unchanged: {ex.Message}"); } catch { } }
    }
    private static void BeforeAction(GameItem item, GameInventory moduleGrid, GameItem machine)
    {
        if (!_actionInstalled || item == null || item.Pointer == IntPtr.Zero || moduleGrid == null || moduleGrid.Pointer == IntPtr.Zero ||
            machine == null || machine.Pointer == IntPtr.Zero ||
            !NativeItemRegistry.IsAppliedItem(item.identifier)) return;
        Actions.Dispatch(item.identifier, new(item, moduleGrid, machine), _log);
    }
}
