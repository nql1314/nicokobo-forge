using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>
/// Build-gated native misc and module item registration for game adapters. The caller owns
/// item construction and effects; Nicokobo Forge owns the directory hook and ID claim.
/// </summary>
internal static class NativeItemRegistry
{
    private sealed record HeldFactory(Func<GameItem> Managed,
        Il2CppSystem.Func<GameItem> Native);

    private static readonly object Gate = new();
    private static readonly NativeItemCatalog Catalog = new();
    private static readonly Dictionary<string, HeldFactory> Factories =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IntPtr> AppliedDirectories =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, NativeApplicationView> Outcomes =
        new(StringComparer.Ordinal);
    private static Action<string>? _log;
    private static bool _enabled;
    private static bool _moduleEnabled;
    private static bool _amenityEnabled;



    internal static bool TryGetAppliedPresentation(string itemId,
        out NativeItemOptions? options, out LocalizedItemText? ownerName)
    {
        lock (Gate)
        {
            options = null;
            ownerName = null;
            if (!AppliedDirectories.ContainsKey(itemId) ||
                !Outcomes.TryGetValue(itemId, out var outcome) || outcome.Status != NativeApplicationStatus.Applied ||
                !Catalog.TryGet(itemId, out var declaration) ||
                declaration == null)
                return false;
            options = declaration.Options;
            ownerName = ForgePresentationApi.OwnerName(declaration.OwnerId);
            return true;
        }
    }

    internal static bool IsAppliedItem(string itemId)
    {
        lock (Gate)
            return AppliedDirectories.ContainsKey(itemId) &&
                Outcomes.TryGetValue(itemId, out var outcome) && outcome.Status == NativeApplicationStatus.Applied &&
                Catalog.TryGet(itemId, out _);
    }

    internal static SubmitResult RegisterNode(string ownerId, string nativeItemId,
        Func<GameItem> factory, NativeItemOptions? options = null)
        => Register(ownerId, nativeItemId, NativeItemKind.Node, factory, options);

    /// <summary>Register a non-node item in MiscItemDirectory, such as a card.</summary>
    internal static SubmitResult RegisterItem(string ownerId, string nativeItemId,
        Func<GameItem> factory, NativeItemOptions? options = null)
        => Register(ownerId, nativeItemId, NativeItemKind.Item, factory, options);

    /// <summary>Register a non-node item in AmenitiesItemDirectory.</summary>
    internal static SubmitResult RegisterAmenity(string ownerId, string nativeItemId,
        Func<GameItem> factory, NativeItemOptions? options = null)
        => Register(ownerId, nativeItemId, NativeItemKind.Amenity, factory, options);

    /// <summary>Register a native machine module in ModuleDirectory.</summary>
    internal static SubmitResult RegisterModule(string ownerId, string nativeItemId,
        Func<GameItem> factory, NativeItemOptions? options = null)
        => Register(ownerId, nativeItemId, NativeItemKind.Module, factory, options);

    /// <summary>Claim all module IDs together, or leave the catalog unchanged.</summary>
    internal static SubmitResult RegisterModules(string ownerId,
        IReadOnlyList<NativeModuleRegistration> modules)
    {
        if (modules == null)
            return new(SubmitStatus.Invalid, "Module batch is null");
        SubmitResult result;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.SubmitBatch(ownerId, modules.Select(module =>
                new NativeItemBatchEntry(module.ItemId, NativeItemKind.Module,
                    module.Factory, module.Options)).ToArray());
            if (result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)
                foreach (var module in modules)
                    Outcomes.TryAdd(module.ItemId, new(ownerId, module.ItemId,
                        NativeItemKind.Module.ToString(),
                        NativeApplicationStatus.Staged, result.Reason));
            log = _log;
        }
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(log, prefix + $"[NicokoboForge/Module] owner={ownerId}; batch={modules.Count}; " +
            $"status={result.Status}; reason={result.Reason}");
        NativeShopAdapter.DeclarationsChanged();
        return result;
    }

    private static SubmitResult Register(string ownerId, string nativeItemId,
        NativeItemKind kind, Func<GameItem> factory, NativeItemOptions? options)
    {
        SubmitResult result;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.Submit(ownerId, nativeItemId, kind, factory, options);
            if (result.Status == SubmitStatus.Accepted)
                Outcomes[nativeItemId] = new(ownerId, nativeItemId, kind.ToString(),
                    NativeApplicationStatus.Staged, result.Reason);
            log = _log;
        }
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(log, prefix + $"[NicokoboForge/{kind}] owner={ownerId}; id={nativeItemId}; " +
            $"status={result.Status}; reason={result.Reason}");
        NativeShopAdapter.DeclarationsChanged();
        return result;
    }

    /// <summary>Last observed directory application per content ID. Staged means
    /// no successful directory application has been observed yet.</summary>
    internal static IReadOnlyList<NativeApplicationView> Snapshot()
    {
        lock (Gate)
            return Outcomes.Values.OrderBy(x => x.ContentId,
                StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<NativeItemDeclaration> Declarations()
    { lock (Gate) return Catalog.Snapshot(); }
    internal static NativeApplicationStatus Outcome(string id)
    { lock (Gate) return Outcomes.TryGetValue(id, out var v) ? v.Status : NativeApplicationStatus.Staged; }

    internal static void SetLogger(Action<string> log)
    {
        IReadOnlyList<NativeItemDeclaration> staged;
        lock (Gate)
        {
            _log = log;
            staged = Catalog.Snapshot();
        }
        foreach (var item in staged)
            SafeLog(log, $"[NicokoboForge/{item.Kind}] owner={item.OwnerId}; id={item.ItemId}; " +
                "status=Staged; directory application pending");
    }

    internal static int StagedCount
    {
        get { lock (Gate) return Catalog.Count; }
    }

    internal static bool ModuleHookInstalled => _moduleEnabled;
    internal static bool AmenityHookInstalled => _amenityEnabled;

    internal static bool Install(HarmonyLib.Harmony harmony, bool allowModule,
        bool allowAmenity)
    {
        _enabled = false;
        _moduleEnabled = false;
        _amenityEnabled = false;
        try
        {
            _enabled = InstallDirectoryHook(harmony, typeof(MiscItemDirectory),
                nameof(MiscDirectoryPostfix), "NativeItem");
            if (!_enabled) return false;
            _moduleEnabled = allowModule && InstallDirectoryHook(harmony,
                typeof(ModuleDirectory), nameof(ModuleDirectoryPostfix), "Module");
            _amenityEnabled = allowAmenity && InstallDirectoryHook(harmony,
                typeof(AmenitiesItemDirectory), nameof(AmenityDirectoryPostfix), "Amenity");
            return true;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/NativeItem] directoryHook=disabled; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static bool InstallDirectoryHook(HarmonyLib.Harmony harmony,
        Type directoryType, string callbackName, string label)
    {
        try
        {
            if (!NativeHookSet.Install("nicokobo.forge.directory." + directoryType.Name.ToLowerInvariant(),
                [new(directoryType, "InitDirectory", [], typeof(void), typeof(NativeItemRegistry), Postfix: callbackName)], _log)) return false;
            SafeLog(_log, $"[NicokoboForge/{label}] directoryHook=installed; buildGated=true");
            return true;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/{label}] directoryHook=disabled; " +
                $"reason={ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void MiscDirectoryPostfix(MiscItemDirectory __instance)
    {
        if (_enabled) OnDirectoryReady(__instance, NativeItemKind.Item);
    }

    private static void ModuleDirectoryPostfix(ModuleDirectory __instance)
    {
        if (_moduleEnabled) OnDirectoryReady(__instance, NativeItemKind.Module);
    }

    private static void AmenityDirectoryPostfix(AmenitiesItemDirectory __instance)
    {
        if (_amenityEnabled) OnDirectoryReady(__instance, NativeItemKind.Amenity);
    }

    private static void OnDirectoryReady(ItemDirectory directory,
        NativeItemKind directoryKind)
    {
        NativeItemDeclaration[] staged;
        lock (Gate)
            staged = Catalog.Snapshot().Where(item => directoryKind switch
            {
                NativeItemKind.Module => item.Kind == NativeItemKind.Module,
                NativeItemKind.Amenity => item.Kind == NativeItemKind.Amenity,
                _ => item.Kind is NativeItemKind.Item or NativeItemKind.Node
            }).ToArray();
        NativeEffectRegistry.OnDirectoryReady();
        foreach (var item in staged)
            Apply(directory, item);
    }

    private static void Apply(ItemDirectory directory, NativeItemDeclaration node)
    {
        try
        {
            if (directory == null || directory.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Native item directory is unavailable");
            if (directory.Has(node.ItemId))
            {
                bool ownPrevious;
                lock (Gate)
                    ownPrevious = AppliedDirectories.TryGetValue(node.ItemId, out var pointer) &&
                        pointer == directory.Pointer;
                SafeLog(_log, $"{(ownPrevious ? "" : "[WARN] ")}[NicokoboForge/{node.Kind}] owner={node.OwnerId}; id={node.ItemId}; " +
                    $"status={(ownPrevious ? "AlreadyApplied" : "Conflict")}; " +
                    "native directory already contains ID");
                SetOutcome(node, ownPrevious ? NativeApplicationStatus.Applied :
                    NativeApplicationStatus.Conflict,
                    ownPrevious ? "Native directory still contains owned ID" :
                        "Native directory already contains ID");
                return;
            }
            if (DirectoryMaster.Has<GameItem>(node.ItemId))
            {
                SetOutcome(node, NativeApplicationStatus.Conflict,
                    "Native item ID exists in another directory");
                SafeLog(_log, $"[WARN] [NicokoboForge/{node.Kind}] owner={node.OwnerId}; " +
                    $"id={node.ItemId}; status=Conflict; native ID exists in another directory");
                return;
            }
            HeldFactory held;
            lock (Gate)
            {
                if (!Factories.TryGetValue(node.ItemId, out held!))
                {
                    var callback = (Func<GameItem>)node.Factory;
                    Func<GameItem> managed = () => CreateChecked(node, callback);
                    var native = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>(managed)
                        ?? throw new InvalidOperationException("Native delegate conversion failed");
                    held = new HeldFactory(managed, native);
                    Factories.Add(node.ItemId, held);
                }
            }
            if (!directory.Add(node.ItemId, held.Native))
                throw new InvalidOperationException("Native directory rejected node factory");
            lock (Gate) AppliedDirectories[node.ItemId] = directory.Pointer;
            SetOutcome(node, NativeApplicationStatus.Applied,
                "Native directory accepted factory");
            SafeLog(_log, $"[NicokoboForge/{node.Kind}] owner={node.OwnerId}; id={node.ItemId}; status=Applied");
        }
        catch (Exception ex)
        {
            SetOutcome(node, NativeApplicationStatus.Failed,
                $"{ex.GetType().Name}: {ex.Message}");
            SafeLog(_log, $"[ERROR] [NicokoboForge/{node.Kind}] owner={node.OwnerId}; id={node.ItemId}; " +
                $"status=Failed; reason={ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SetOutcome(NativeItemDeclaration item,
        NativeApplicationStatus status, string reason)
    {
        lock (Gate)
            Outcomes[item.ItemId] = new(item.OwnerId, item.ItemId,
                item.Kind.ToString(), status, reason);
        ForgeContentApi.Changed(new(item.OwnerId, item.ItemId, item.Kind.ToString(), status, reason));
    }

    private static GameItem CreateChecked(NativeItemDeclaration node, Func<GameItem> callback)
    {
        try
        {
            var item = callback();
            if (item == null || item.Pointer == IntPtr.Zero ||
                item.identifier != node.ItemId ||
                (node.Kind == NativeItemKind.Node && !item.IsGameItemType("NODE")) ||
                (node.Kind == NativeItemKind.Module && !item.IsGameItemType("MODULE")) ||
                (node.Kind is NativeItemKind.Item or NativeItemKind.Amenity &&
                 (item.IsGameItemType("NODE") || item.IsGameItemType("MODULE"))))
                throw new InvalidOperationException("Item factory returned an invalid item, ID or type");
            return item;
        }
        catch (Exception ex)
        {
            SafeLog(_log, $"[ERROR] [NicokoboForge/{node.Kind}] owner={node.OwnerId}; id={node.ItemId}; " +
                $"status=FactoryFailed; reason={ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static void SafeLog(Action<string>? log, string message)
    {
        try { log?.Invoke(message); }
        catch { /* Logging must not affect native registration. */ }
    }
}
