using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;
using System.Text.Json;
using UnityEngine;

namespace Nicokobo.Forge;

/// <summary>Explicit source-verified recovery; success means staged memory only.</summary>
public sealed record ForgeRunForkRepairResult(bool ReboundInMemory, string Reason);

/// <summary>Distinguishes genuine starts from InitialSave continuations and
/// migrates registered content before the nested native save.</summary>
public static class ForgeRunForkApi
{
    private const string HookId = "nicokobo.forge.run_fork";
    private sealed record Participant(string Owner, string Key,
        Func<string, ForgeRunIdentity, ForgeRunIdentity, string> Rebind,
        Action<PlayerStore>? Rebound, Func<PlayerStore, bool>? Ready);
    private sealed class Attempt(PlayerStore store, ForgeRunIdentity source,
        (Participant Handler, string Json)[] states)
    {
        internal readonly PlayerStore Store = store;
        internal readonly ForgeRunIdentity Source = source;
        internal readonly (Participant Handler, string Json)[] States = states;
        internal ForgeRunIdentity? Destination;
        internal Dictionary<string, string>? ExpectedWrite;
        internal bool WriteCompleted;
    }
    private static readonly Dictionary<string, Participant> Participants = new(StringComparer.Ordinal);
    private static IDisposable? _loaded;
    private static Action<string>? _log;
    private static (IntPtr Store, ForgeRunIdentity Identity)? _known;
    private static IntPtr _freshStore, _blockedStore;
    [ThreadStatic] private static Attempt? _attempt;
    [ThreadStatic] private static int _initialDepth;
    public static bool IsAvailable { get; private set; }

    /// <summary>Rebind is pure and validates the entire owned state, including
    /// unresolved transactions. It runs once with source==destination before
    /// allocation, then with the actual destination before the inner SaveGame.
    /// Rebound invalidates live caches; it must not grant, save, or replay actions.</summary>
    public static IDisposable RegisterState(string ownerId, string key,
        Func<string, ForgeRunIdentity, ForgeRunIdentity, string> rebind,
        Action<PlayerStore>? rebound = null, Func<PlayerStore, bool>? ready = null)
    {
        if (!IsAvailable) throw new InvalidOperationException("Run-fork adapter unavailable");
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, key) || rebind == null)
            throw new ArgumentException("Invalid run-fork owner, key or callback");
        var participant = new Participant(ownerId, key, rebind, rebound, ready);
        if (!Participants.TryAdd(key, participant)) throw new InvalidOperationException("Run-fork key already registered: " + key);
        return new CallbackLease(() => { if (ReferenceEquals(Participants.GetValueOrDefault(key), participant)) Participants.Remove(key); });
    }

    public static bool IsForking(PlayerStore store) => IsAvailable &&
        _attempt is { } attempt && store.Pointer == attempt.Store.Pointer;

    /// <summary>Explicit recovery for an already mismatched branch. Every
    /// registered payload must match the caller-selected original snapshot
    /// byte-for-byte and pass its owner's validation. Never saves a file.</summary>
    public static ForgeRunForkRepairResult RepairFromSnapshot(PlayerStore store, string sourceSnapshotPath)
    {
        if (!IsAvailable || PlayerStore.instance?.Pointer != store.Pointer || _attempt != null || _initialDepth != 0 || _blockedStore == store.Pointer ||
            _known is not { } known || known.Store != store.Pointer || !Same(known.Identity, Identity(store)))
            return new(false, "A verified loaded destination is required");
        using var read = ForgeSaveReadbackApi.ReadFile(sourceSnapshotPath);
        if (read.Status != ForgeSaveReadStatus.Readable) return new(false, "Source snapshot unavailable: " + read.Status);
        try
        {
            var wrapper = ForgeSaveReadbackApi.RequireProperty(read.Root, "playerStore");
            if (ForgeSaveReadbackApi.RequireProperty(wrapper, "__type").GetString() != "PlayerStore,Assembly-CSharp")
                return new(false, "Unexpected source snapshot type");
            var sourceStore = ForgeSaveReadbackApi.RequireProperty(wrapper, "value");
            var source = new ForgeRunIdentity(ForgeSaveReadbackApi.RequireProperty(sourceStore, "runID").GetString()!,
                ForgeSaveReadbackApi.RequireProperty(sourceStore, "saveSlotId").GetInt32(), StoreStation.GetDayCounter());
            if (!Valid(source) || Same(source, Identity(store)) ||
                ForgeSaveReadbackApi.RequireProperty(sourceStore, "startType").GetInt32() != (int)store.startType)
                return new(false, "Source identity or starting career does not match a damaged branch");
            var data = ForgeSaveReadbackApi.RequireProperty(sourceStore, "modData");
            var states = new List<(Participant, string)>();
            foreach (var handler in Participants.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray())
            {
                if (handler.Ready?.Invoke(store) == false) return new(false, "Participant is busy: " + handler.Owner);
                if (!store.modData.ContainsKey(handler.Key)) continue;
                string json = store.modData[handler.Key];
                if (ForgeSaveReadbackApi.RequireProperty(data, handler.Key).GetString() != json)
                    return new(false, "Source snapshot does not prove this state: " + handler.Key);
                RequireJson(handler.Rebind(json, source, source));
                states.Add((handler, json));
            }
            if (states.Count == 0) return new(false, "No owned progress to repair");
            _attempt = new(store, source, states.ToArray());
            SavePrefix(store); // Atomic in-memory migration only; no native save call.
            return new(true, "Source progress verified and rebound in memory; pending normal game save");
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or JsonException or FormatException)
        { return new(false, ex.Message); }
        finally { _attempt = null; }
    }

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed || IsAvailable) return;
        var harmony = new HarmonyLib.Harmony(HookId);
        try
        {
            var hooks = new[]
            {
                (nameof(PlayerStore.StartNewGame), nameof(NewGamePrefix), (string?)null, (string?)null),
                (nameof(PlayerStore.InitialSave), nameof(InitialPrefix), (string?)nameof(InitialPostfix), (string?)nameof(InitialFinalizer)),
                (nameof(PlayerStore.SaveGame), nameof(SavePrefix), (string?)null, (string?)null),
                ("WriteSlotFile", nameof(WritePrefix), (string?)nameof(WritePostfix), (string?)null)
            }.Select(h => (Target: NativeHookSet.Require(typeof(PlayerStore), h.Item1, Type.EmptyTypes, typeof(void)),
                Prefix: Callback(h.Item2), Postfix: Callback(h.Item3), Finalizer: Callback(h.Item4))).ToArray();
            foreach (var hook in hooks) NativeHookSet.RequireNativeEntryPoint(hook.Target);
            foreach (var hook in hooks) harmony.Patch(hook.Target, hook.Prefix, hook.Postfix, finalizer: hook.Finalizer);
            _loaded = ForgeLifecycleApi.Subscribe("nicokobo.forge", HookId + ".loaded",
                ForgeLifecyclePhase.AfterGameLoadedLate, context => MarkLoaded(context.Store));
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            harmony.UnpatchSelf(); _loaded?.Dispose(); _loaded = null;
            Log("[ERROR] Run-fork adapter disabled: " + ex.Message);
        }
    }

    internal static void Uninstall()
    {
        IsAvailable = false; _loaded?.Dispose(); _loaded = null;
        NativeHookSet.Remove(HookId, _log); Participants.Clear();
        _attempt = null; _known = null; _freshStore = _blockedStore = IntPtr.Zero; _initialDepth = 0;
    }

    private static HarmonyMethod? Callback(string? name) => name == null ? null :
        new(AccessTools.Method(typeof(ForgeRunForkApi), name)) { priority = Priority.First };
    private static ForgeRunIdentity Identity(PlayerStore store) => new(store.runID, store.saveSlotId, StoreStation.GetDayCounter());
    private static bool Valid(ForgeRunIdentity id) => !string.IsNullOrWhiteSpace(id.RunId) && id.SlotId >= 0 && id.Day >= 0;
    private static bool Same(ForgeRunIdentity a, ForgeRunIdentity b) => a.RunId == b.RunId && a.SlotId == b.SlotId;
    private static void MarkLoaded(PlayerStore store)
    {
        if (PlayerStore.instance?.Pointer != store.Pointer) return;
        var identity = Identity(store);
        if (Valid(identity)) { _known = (store.Pointer, identity); _blockedStore = IntPtr.Zero; _freshStore = IntPtr.Zero; }
    }
    private static void NewGamePrefix(PlayerStore __instance)
    {
        if (_attempt != null) throw new InvalidOperationException("Cannot start a new game during a run fork");
        _known = null; _blockedStore = IntPtr.Zero; _freshStore = __instance.Pointer;
    }

    private static void InitialPrefix(PlayerStore __instance, ref bool __state)
    {
        __state = false;
        if (_initialDepth != 0) throw new InvalidOperationException("Recursive InitialSave is not a valid run fork");
        CheckNotBlocked(__instance);
        var source = Identity(__instance);
        bool fresh = _freshStore == __instance.Pointer || !Valid(source);
        if (!fresh)
        {
            if (_known is not { } known || known.Store != __instance.Pointer || !Same(known.Identity, source) ||
                PlayerStore.instance?.Pointer != __instance.Pointer || __instance.modData == null)
                throw new InvalidOperationException("InitialSave continuation has no verified loaded source");
            var states = new List<(Participant, string)>();
            foreach (var handler in Participants.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray())
            {
                if (handler.Ready?.Invoke(__instance) == false)
                    throw new InvalidOperationException("Run-fork participant is busy: " + handler.Owner);
                if (!__instance.modData.ContainsKey(handler.Key)) continue;
                string json = __instance.modData[handler.Key];
                RequireJson(handler.Rebind(json, source, source));
                if (!Same(Identity(__instance), source) || !__instance.modData.ContainsKey(handler.Key) || __instance.modData[handler.Key] != json)
                    throw new InvalidOperationException("Run-fork source changed during validation");
                states.Add((handler, json));
            }
            _attempt = new(__instance, source, states.ToArray());
        }
        _freshStore = IntPtr.Zero; _initialDepth++; __state = true;
    }

    private static void SavePrefix(PlayerStore __instance)
    {
        CheckNotBlocked(__instance);
        CheckVerifiedAddress(__instance);
        if (_attempt is not { } attempt || attempt.Store.Pointer != __instance.Pointer) return;
        var destination = Identity(__instance);
        if (PlayerStore.instance?.Pointer != __instance.Pointer || !Valid(destination) ||
            destination.Day != attempt.Source.Day || Same(destination, attempt.Source))
            throw new InvalidOperationException("InitialSave did not allocate a new run identity");
        if (attempt.Destination is { } previous)
        {
            if (!Same(destination, previous)) throw new InvalidOperationException("Run-fork destination changed twice");
            return;
        }
        var writes = new List<(Participant Handler, string Before, string After)>();
        try
        {
            foreach (var (handler, json) in attempt.States)
            {
                if (!__instance.modData.ContainsKey(handler.Key) || __instance.modData[handler.Key] != json)
                    throw new InvalidOperationException("Run-fork state changed: " + handler.Key);
                string next = handler.Rebind(json, attempt.Source, destination);
                RequireJson(next); writes.Add((handler, json, next));
            }
            if (!Same(destination, Identity(__instance))) throw new InvalidOperationException("Run-fork native identity changed");
            foreach (var write in writes)
                if (!__instance.modData.ContainsKey(write.Handler.Key) || __instance.modData[write.Handler.Key] != write.Before)
                    throw new InvalidOperationException("Run-fork source revision changed");
            foreach (var write in writes) __instance.modData[write.Handler.Key] = write.After;
            foreach (var write in writes)
                if (__instance.modData[write.Handler.Key] != write.After) throw new InvalidOperationException("Run-fork staging readback failed");
            attempt.Destination = destination;
            foreach (var write in writes) write.Handler.Rebound?.Invoke(__instance);
            if (!Same(destination, Identity(__instance))) throw new InvalidOperationException("Run-fork rebound changed native identity");
            _known = (__instance.Pointer, destination);
            Log($"[INFO] Run fork rebound before save: {attempt.Source.SlotId} -> {destination.SlotId}; states={writes.Count}");
        }
        catch
        {
            foreach (var write in writes)
                if (__instance.modData.ContainsKey(write.Handler.Key) && __instance.modData[write.Handler.Key] == write.After)
                    __instance.modData[write.Handler.Key] = write.Before;
            _blockedStore = __instance.Pointer;
            throw;
        }
    }
    private static void WritePrefix(PlayerStore __instance)
    {
        CheckNotBlocked(__instance);
        CheckVerifiedAddress(__instance);
        if (IsForking(__instance) && (_attempt!.Destination == null || PlayerStore.instance?.Pointer != __instance.Pointer ||
            !Same(_attempt.Destination, Identity(__instance))))
            throw new InvalidOperationException("Run-fork state must be migrated before WriteSlotFile");
        if (IsForking(__instance))
            _attempt!.ExpectedWrite = _attempt.States.ToDictionary(x => x.Handler.Key, x => __instance.modData[x.Handler.Key], StringComparer.Ordinal);
    }
    private static void WritePostfix(PlayerStore __instance, bool __runOriginal)
    {
        if (!__runOriginal || !IsForking(__instance)) return;
        var attempt = _attempt!;
        var identity = attempt.Destination ?? throw new InvalidOperationException("Missing fork destination");
        using var read = ForgeSaveReadbackApi.ReadFile(Path.Combine(Application.persistentDataPath, SaveFiles.Slot(identity.SlotId)));
        if (read.Status != ForgeSaveReadStatus.Readable) throw new InvalidDataException("Fork save readback failed: " + read.Status);
        var wrapper = ForgeSaveReadbackApi.RequireProperty(read.Root, "playerStore");
        if (ForgeSaveReadbackApi.RequireProperty(wrapper, "__type").GetString() != "PlayerStore,Assembly-CSharp")
            throw new InvalidDataException("Fork save has an unexpected native type");
        var saved = ForgeSaveReadbackApi.RequireProperty(wrapper, "value");
        if (ForgeSaveReadbackApi.RequireProperty(saved, "runID").GetString() != identity.RunId ||
            ForgeSaveReadbackApi.RequireProperty(saved, "saveSlotId").GetInt32() != identity.SlotId)
            throw new InvalidDataException("Fork save identity did not reach disk");
        var data = ForgeSaveReadbackApi.RequireProperty(saved, "modData");
        foreach (var pair in attempt.ExpectedWrite ?? throw new InvalidOperationException("Missing fork write snapshot"))
            if (ForgeSaveReadbackApi.RequireProperty(data, pair.Key).GetString() != pair.Value)
                throw new InvalidDataException("Fork state did not reach disk: " + pair.Key);
        attempt.WriteCompleted = true;
    }
    private static void InitialPostfix(PlayerStore __instance, bool __runOriginal)
    {
        if (!__runOriginal) return;
        if (IsForking(__instance) && (_attempt!.Destination == null || !_attempt.WriteCompleted))
            throw new InvalidOperationException("Run fork returned without preparing its native save");
        MarkLoaded(__instance);
    }
    private static Exception? InitialFinalizer(PlayerStore __instance, bool __state, Exception? __exception)
    {
        if (!__state) return __exception;
        if (__exception != null && _attempt is { } attempt && !Same(Identity(__instance), attempt.Source))
        {
            _blockedStore = __instance.Pointer;
            Log("[ERROR] Run fork failed after native allocation; further saves are blocked. Reload the source save; no automatic retry. " + __exception.Message);
        }
        _attempt = null; _initialDepth = 0;
        return __exception;
    }
    private static void CheckNotBlocked(PlayerStore store)
    {
        if (_blockedStore == store.Pointer) throw new InvalidOperationException("Failed run fork requires reloading its source save");
    }
    private static void CheckVerifiedAddress(PlayerStore store)
    {
        if (_attempt == null && _initialDepth == 0 && _known is { } known && known.Store == store.Pointer &&
            !Same(known.Identity, Identity(store)))
            throw new InvalidOperationException("Native run/slot changed without a verified load or fork; save refused");
    }
    private static void RequireJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > ForgeNumbers.RunData.MaxJsonChars) throw new InvalidDataException("Invalid run-fork JSON size");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = ForgeNumbers.RunData.MaxJsonDepth });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Run-fork state must be an object");
    }
    private static void Log(string message) { try { _log?.Invoke(message); } catch { } }
}
