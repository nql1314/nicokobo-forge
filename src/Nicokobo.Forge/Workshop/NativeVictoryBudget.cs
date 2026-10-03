using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge.Workshop;

// The built-in victory reward owns this multiplier; ordinary sales keep using
// the native balance, affordability check and ModBudget deduction.
internal static class NativeVictoryBudget
{
    private const string HookId = "nicokobo.forge.native_workshop.victory_budget";
    private static readonly HashSet<IntPtr> ScaledClients = [];
    private static bool _active;
    private static Action<string>? _log;
    internal static bool Installed { get; private set; }

    internal static void Install(Action<string> log)
    {
        if (Installed) return;
        _log = log;
        Installed = NativeHookSet.Install(HookId,
        [
            new(typeof(StoreClient), nameof(StoreClient.SetBudget), [typeof(int)], typeof(void),
                typeof(NativeVictoryBudget), Postfix: nameof(InitializedPostfix)),
            new(typeof(StoreClient), nameof(StoreClient.SetBudget), [typeof(int), typeof(int)], typeof(void),
                typeof(NativeVictoryBudget), Postfix: nameof(InitializedPostfix)),
            new(typeof(StoreClient), nameof(StoreClient.SetClientBudget), [typeof(int), typeof(int)], typeof(void),
                typeof(NativeVictoryBudget), Postfix: nameof(InitializedPostfix)),
            new(typeof(StoreClientInstance), nameof(StoreClientInstance.CreateClientInstance), [typeof(StoreClient)],
                typeof(StoreClientInstance), typeof(NativeVictoryBudget), Prefix: nameof(ArrivalPrefix))
        ], log);
    }

    internal static void Reset()
    {
        _active = false;
        ScaledClients.Clear();
    }

    internal static void SetActive(bool claimed, StoreClient? currentClient)
    {
        _active = Installed && claimed;
        Apply(currentClient);
    }

    [HarmonyAfter("nicokobo.mechcore.synthesis.aug_trading")]
    private static void InitializedPostfix(StoreClient __instance)
    {
        if (__instance == null || __instance.Pointer == IntPtr.Zero) return;
        // These setters assign a fresh native budget. A replacement budget
        // receives the reward again, without scaling its previous balance.
        ScaledClients.Remove(__instance.Pointer);
        Apply(__instance);
    }

    private static void ArrivalPrefix(StoreClient __0) => Apply(__0);

    private static void Apply(StoreClient? client)
    {
        if (!_active || client == null || client.Pointer == IntPtr.Zero ||
            !client.useClientBudget || ScaledClients.Contains(client.Pointer)) return;
        try
        {
            int budget = client.GetBudget();
            if (budget > 0)
                client.OverrideBudget((int)Math.Min(int.MaxValue,
                    (long)budget * ForgeNumbers.Achievements.VictoryBudgetMultiplier));
            // Arrival and repeated save confirmations may see the same client
            // after a sale. They must never multiply or refill that balance.
            ScaledClients.Add(client.Pointer);
        }
        catch (Exception ex)
        {
            _log?.Invoke("[WARN] [NicokoboForge/Workshop] victory budget unavailable: " + ex.Message);
        }
    }
}
