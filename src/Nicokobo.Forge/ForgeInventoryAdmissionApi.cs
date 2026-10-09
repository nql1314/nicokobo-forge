using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Adds content-owned restrictions after a native grid's admission.
/// A restriction cannot override native ownership, locks, cycles or shape rules.
/// Callbacks remain managed; never store them in native admission fields, whose
/// DynamicInvoke path cannot invoke an Il2CppToMonoDelegateReference target.</summary>
public static class ForgeInventoryAdmissionApi
{
    private sealed record Rule(string InventoryId, Func<GameItem, GameGridInventory, bool> Allows);
    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal);
    private static Action<string>? _log;
    public static bool IsAvailable { get; private set; }

    /// <summary>Restrict one inventory ID directly owned by this item ID.
    /// Keep the lease while these inventories are in use. The native grid still
    /// performs all normal checks and the actual transfer. This API does not
    /// authorize UncheckedAccept or modify any native callback field.</summary>
    public static IDisposable Register(string ownerId, string itemId, string inventoryId,
        Func<GameItem, GameGridInventory, bool> allows)
    {
        if (!IsAvailable) throw new InvalidOperationException("Inventory admission adapter unavailable");
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, itemId) || string.IsNullOrWhiteSpace(inventoryId) ||
            allows == null || !Rules.TryAdd(itemId, new(inventoryId, allows)))
            throw new ArgumentException("Unique owned root, inventory ID and admission callback required");
        return new CallbackLease(() => Rules.Remove(itemId));
    }

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) return;
        IsAvailable = NativeHookSet.Install("nicokobo.forge.inventory.admission",
        [new(typeof(GameGridInventory), nameof(GameGridInventory.MayHaveValidInventorySlot), [typeof(GameItem)],
            typeof(bool), typeof(ForgeInventoryAdmissionApi), Postfix: nameof(AfterAdmission))], log);
    }

    private static void AfterAdmission(GameGridInventory __instance, GameItem __0, ref bool __result)
    {
        if (!__result || !IsAvailable) return;
        try
        {
            var owner = __instance.GetParentItem();
            string? itemId = owner?.identifier;
            if (string.IsNullOrEmpty(itemId) || !Rules.TryGetValue(itemId, out var rule) ||
                __instance.identifier != rule.InventoryId) return;
            __result = ForgeInventoryApi.IsPlayerOwned(owner!) && rule.Allows(__0, __instance);
        }
        catch (Exception ex)
        {
            __result = false;
            try { _log?.Invoke("[WARN] [NicokoboForge/Admission] grid insertion rejected: " + ex.Message); }
            catch { }
        }
    }
}
