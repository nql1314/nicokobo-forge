using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Transient movement leases for real instances and their containing
/// devices. A lease is not serialized and never consumes inventory.</summary>
public static class ForgeInventoryFreezeApi
{
    private static readonly Dictionary<IntPtr, (string? Group, int Count)> Frozen = [];
    public static bool IsAvailable { get; private set; }
    public static bool IsFrozen(GameItem item) => item != null && Frozen.ContainsKey(item.Pointer);
    /// <summary>A named group may share containing ancestors among its own
    /// leases. Anonymous transactions and different groups remain exclusive.</summary>
    public static IDisposable? TryAcquire(IEnumerable<GameItem> items, string? group = null)
    {
        if (!IsAvailable || items == null) return null;
        var snapshot = items.ToArray();
        if (snapshot.Any(item => item == null || item.Pointer == IntPtr.Zero)) return null;
        var pointers = snapshot.Select(item => item.Pointer).Distinct().ToArray();
        lock (Frozen)
        {
            if (pointers.Any(pointer => Frozen.TryGetValue(pointer, out var held) &&
                (group == null || held.Group != group))) return null;
            foreach (var pointer in pointers)
                Frozen[pointer] = Frozen.TryGetValue(pointer, out var held) ? (held.Group, checked(held.Count + 1)) : (group, 1);
        }
        return new CallbackLease(() =>
        {
            lock (Frozen) foreach (var pointer in pointers)
            {
                if (!Frozen.TryGetValue(pointer, out var held)) continue;
                if (held.Count == 1) Frozen.Remove(pointer); else Frozen[pointer] = (held.Group, held.Count - 1);
            }
        });
    }
    internal static void Install(bool allowed, Action<string> log)
    {
        if (!allowed) return;
        IsAvailable = NativeHookSet.Install("nicokobo.forge.inventory.freeze",
        [
            new(typeof(GameItem), nameof(GameItem.MayRemove), [], typeof(bool), typeof(ForgeInventoryFreezeApi), nameof(BeforeRemove)),
            new(typeof(GameGridInventory), nameof(GameGridInventory.Expel), [typeof(GameItem)], typeof(bool), typeof(ForgeInventoryFreezeApi), nameof(BeforeExpel)),
            new(typeof(GameGridScrollableInventory), nameof(GameGridScrollableInventory.Expel), [typeof(GameItem)], typeof(bool), typeof(ForgeInventoryFreezeApi), nameof(BeforeExpel)),
            new(typeof(GameSlotInventory), nameof(GameSlotInventory.Expel), [typeof(GameItem)], typeof(bool), typeof(ForgeInventoryFreezeApi), nameof(BeforeExpel))
        ], log);
    }
    private static bool BeforeRemove(GameItem __instance, ref bool __result)
    { if (!IsFrozen(__instance)) return true; __result = false; return false; }
    private static bool BeforeExpel(GameItem __0, ref bool __result)
    { if (!IsFrozen(__0)) return true; __result = false; return false; }
}
