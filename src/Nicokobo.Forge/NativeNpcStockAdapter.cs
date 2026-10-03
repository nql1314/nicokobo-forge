using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Nicokobo.Forge;

// Supply runs on dialogue callbacks as well as arrival. Scope the reviewed
// callbacks. General suppliers get one extra offer; miners replace their ore batch.
internal static class NativeNpcStockAdapter
{
    private sealed class SupplyScope(NpcTradeStockCategory categories, bool replaceOreBatch = false)
    {
        internal NpcTradeStockCategory Categories { get; } = categories;
        internal bool ReplaceOreBatch { get; } = replaceOreBatch;
        internal bool Attempted { get; set; }
        internal string? SelectedOreId { get; set; }
    }
    private sealed record StockPlacement(IntPtr Pointer, string ItemId);
    [ThreadStatic] private static SupplyScope? _supply;
    private static bool _allowed;
    private static bool _installationPending;
    private static bool _installationAttempted;
    private static Action<string>? _log;
    internal static bool Installed { get; private set; }

    internal static void Configure(bool allowed, Action<string> log)
    {
        _allowed = allowed;
        _log = log;
        DeclarationsChanged();
    }

    internal static void DeclarationsChanged()
    {
        if (!_allowed || _installationAttempted ||
            !NativeItemRegistry.Declarations().Any(item => item.Options.NpcTrade != null)) return;
        // Resolving closure metadata initializes StoreClientListSpec, whose
        // static constructor reads localized dialogue synchronously. Queue
        // hooks until the game's existing localization operation has finished.
        _installationPending = true;
    }

    internal static void Update()
    {
        if (!_allowed || !_installationPending || _installationAttempted) return;
        try
        {
            if (!LocalizationSettings.HasSettings) return;
            // Read the existing handle instead of starting initialization or
            // calling WaitForCompletion from a Mod registration callback.
            var operation = LocalizationSettings.Instance.m_InitializingOperationHandle;
            if (operation == null || !operation.IsValid() || !operation.IsDone) return;
            _installationPending = false;
            _installationAttempted = true;
            if (operation.Status != AsyncOperationStatus.Succeeded)
            {
                SafeLog("[WARN] [NicokoboForge/NpcStock] disabled: native localization initialization failed");
                return;
            }
            Install();
        }
        catch (Exception ex)
        {
            _installationPending = false;
            _installationAttempted = true;
            SafeLog($"[WARN] [NicokoboForge/NpcStock] disabled: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Install()
    {
        var hooks = new List<NativeHook> {
            new(typeof(StoreClientList), nameof(StoreClientList.PlaceInventorInventory),
                [typeof(bool)], typeof(void), typeof(NativeNpcStockAdapter),
                Prefix: nameof(InventorStockPrefix), Finalizer: nameof(EndSupply)),
            Supply(typeof(StoreClientList), nameof(StoreClientList.PlaceSupplierInventory), nameof(GeneralStockPrefix)),
            new(typeof(PlayerStore), nameof(PlayerStore.AddDirectSellingItemToTable),
                [typeof(GameItem), typeof(bool), typeof(bool), typeof(bool), typeof(int)],
                typeof(void), typeof(NativeNpcStockAdapter), Prefix: nameof(PlaceStockPrefix),
                Postfix: nameof(StockPlacedPostfix))
        };
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(MinerStockPrefix), "_CreateMiner_b__38_0");
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(MaterialStockPrefix), "_CreateJunker_b__21_0");
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(TechnicalStockPrefix),
            "_CreateScrapper_b__35_0", "_CreateLowerLevelRareMerchant_b__64_0", "_CreateInventorStorage_b__31_0");
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(GeneralStockPrefix),
            "_CreateThief_b__47_0", "_CreatePettyThief_b__48_0", "_CreateBrokeUpperLevel_b__69_0");
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(FoodStockPrefix), "_CreateFoodThief_b__49_0");
        Suppliers(hooks, typeof(StoreClientList.__c), nameof(MedicalStockPrefix),
            "_CreateShadyPharmacist_b__34_0", "_CreateScavBlood_b__58_0", "_CreateRareLowerLevelChemist_b__65_0");
        Suppliers(hooks, typeof(StoreClientList.__c__DisplayClass22_0), nameof(MaterialStockPrefix), "_CreateJunkerSellOnly_b__0");
        Suppliers(hooks, typeof(StoreClientList.__c__DisplayClass37_0), nameof(MedicalStockPrefix), "_CreateLowerLevelChemist_b__0");
        Suppliers(hooks, typeof(StoreClientList.__c__DisplayClass41_0), nameof(GeneralStockPrefix), "_CreateScavGeneral_b__0");
        Suppliers(hooks, typeof(StoreClientList.__c__DisplayClass42_0), nameof(GeneralStockPrefix), "_CreateScavCrate_b__0");
        Suppliers(hooks, typeof(StoreClientListMinor.__c), nameof(GeneralStockPrefix),
            "_CreateScavHaul_b__8_0", "_CreateSalvagePilot_b__14_0", "_CreateOldScav_b__17_0", "_CreateConspiracyClient_b__22_0");
        Suppliers(hooks, typeof(StoreClientListMinor.__c), nameof(MaterialStockPrefix), "_CreatePeatClient_b__21_0");
        Suppliers(hooks, typeof(StoreClientListMinor.__c), nameof(MedicalStockPrefix), "_CreateNurse1_b__5_0");
        Suppliers(hooks, typeof(StoreClientListRev.__c), nameof(GeneralStockPrefix), "_CreateRevRaider_b__0_0", "_CreateRevQuartermaster_b__1_0");
        Suppliers(hooks, typeof(StoreClientListSpec.__c), nameof(FoodStockPrefix), "_CreateRetiredWinemaker_b__4_0");
        Suppliers(hooks, typeof(StoreClientListSpec.__c), nameof(MaterialStockPrefix), "_CreateRetiredJunker_b__5_0");
        Suppliers(hooks, typeof(StoreClientListSpec.__c), nameof(MedicalStockPrefix), "_CreateRetiredChemist_b__7_0");
        Suppliers(hooks, typeof(StoreClientListSurplus.__c), nameof(FoodStockPrefix), "_CreateFoodSurplusClient_b__0_0", "_CreateFoodSurplusClient_b__0_1");
        Suppliers(hooks, typeof(StoreClientListSurplus.__c), nameof(MedicalStockPrefix), "_CreateMedicalSurplusClient_b__1_0", "_CreateMedicalSurplusClient_b__1_1");
        Suppliers(hooks, typeof(StoreClientListSurplus.__c), nameof(MaterialStockPrefix), "_CreateMaterialSurplusClient_b__4_0", "_CreateMaterialSurplusClient_b__4_1");
        Suppliers(hooks, typeof(StoreClientListEvent.__c), nameof(HouseholdStockPrefix), "_CreateScavengerHouseholdClient_b__8_0");
        Installed = NativeHookSet.Install("nicokobo.forge.npc_stock", hooks, _log);
        ForgeCapabilities.Publish(ForgeCapabilities.Current with { NpcTradeStock = Installed });
        SafeLog($"[NicokoboForge/NpcStock] installed={Installed}; supplyHooks={hooks.Count - 1}");
    }

    private static NativeHook Supply(Type type, string method, string callback) =>
        new(type, method, [], typeof(void), typeof(NativeNpcStockAdapter),
            Prefix: callback, Finalizer: nameof(EndSupply));
    private static void Suppliers(List<NativeHook> hooks, Type type, string callback, params string[] methods) =>
        hooks.AddRange(methods.Select(method => Supply(type, method, callback)));

    private static void BeginSupply(NpcTradeStockCategory categories, out SupplyScope? previous,
        bool replaceOreBatch = false)
    {
        previous = _supply;
        if (Installed) _supply = new(categories, replaceOreBatch);
    }
    private static void EndSupply(SupplyScope? __state) => _supply = __state;
    private static void MinerStockPrefix(out SupplyScope? __state) => BeginSupply(NpcTradeStockCategory.Ore, out __state, true);
    private static void MaterialStockPrefix(out SupplyScope? __state) => BeginSupply(
        NpcTradeStockCategory.Material | NpcTradeStockCategory.Ore | NpcTradeStockCategory.Module, out __state);
    private static void TechnicalStockPrefix(out SupplyScope? __state) => BeginSupply(
        NpcTradeStockCategory.Material | NpcTradeStockCategory.Module |
        NpcTradeStockCategory.Machine | NpcTradeStockCategory.Household, out __state);
    private static void GeneralStockPrefix(out SupplyScope? __state) => BeginSupply(NativeNpcStockPolicy.All, out __state);
    private static void FoodStockPrefix(out SupplyScope? __state) => BeginSupply(NpcTradeStockCategory.Food, out __state);
    private static void MedicalStockPrefix(out SupplyScope? __state) => BeginSupply(NpcTradeStockCategory.Medical, out __state);
    private static void HouseholdStockPrefix(out SupplyScope? __state) => BeginSupply(NpcTradeStockCategory.Household, out __state);
    private static void InventorStockPrefix(bool isVisitingPlayerStore, out SupplyScope? __state)
    {
        __state = _supply;
        _supply = null;
        if (isVisitingPlayerStore) TechnicalStockPrefix(out _);
    }

    private static bool PlaceStockPrefix(ref GameItem __0, bool __1, out StockPlacement? __state)
    {
        __state = null;
        if (!Installed || _supply is not { } supply ||
            __1 || __0 == null || __0.Pointer == IntPtr.Zero) return true;
        if (supply.ReplaceOreBatch)
            return ReplaceMinerOre(supply, ref __0, out __state);
        if (supply.Attempted) return true;
        // Set this before recursive native placement of the custom item. The
        // scope ends on normal return or exception, so refreshes draw again.
        supply.Attempted = true;
        AddStock(supply.Categories);
        return true;
    }

    private static bool ReplaceMinerOre(SupplyScope supply, ref GameItem original,
        out StockPlacement? placement)
    {
        placement = null;
        if (original.identifier != NativeNpcStockPolicy.NativeMinerOreId) return true;
        GameItem? replacement = null;
        try
        {
            var store = PlayerStore.instance;
            if (store == null || store.Pointer == IntPtr.Zero)
                throw new InvalidOperationException("Current miner store is unavailable");
            if (!supply.Attempted)
            {
                supply.Attempted = true;
                supply.SelectedOreId = NativeNpcStockPolicy.SelectMinerOre(
                    StockOffers(NpcTradeStockCategory.Ore, store), RNG.GetRandomDouble(0, 1))
                    ?? throw new InvalidOperationException("Miner ore draw failed");
                SafeLog($"[NicokoboForge/NpcStock] client=miner; mode=OreBatch; id={supply.SelectedOreId}");
            }
            if (supply.SelectedOreId == NativeNpcStockPolicy.NativeMinerOreId) return true;
            if (supply.SelectedOreId == null)
                throw new InvalidOperationException("Miner batch selection is unavailable");
            if (original.parentInventory != null || original.unitCount <= 0)
                throw new InvalidOperationException("Miner stock must be detached with a positive quantity");
            replacement = DirectoryMaster.Item(supply.SelectedOreId, true);
            if (replacement == null || replacement.Pointer == IntPtr.Zero ||
                replacement.Pointer == original.Pointer || replacement.parentInventory != null ||
                replacement.identifier != supply.SelectedOreId)
                throw new InvalidOperationException("Miner ore factory contract failed");
            if (replacement.unitCount != original.unitCount) replacement.SetUnitCount(original.unitCount);
            if (replacement.unitCount != original.unitCount)
                throw new InvalidOperationException("Miner ore quantity could not be preserved");
        }
        catch (Exception ex)
        {
            if (replacement != null && replacement.Pointer != IntPtr.Zero &&
                replacement.Pointer != original.Pointer && replacement.parentInventory == null)
                DestroyUnusedStock(replacement);
            DestroyUnusedStock(original);
            SafeLog($"[WARN] [NicokoboForge/NpcStock] client=miner; mode=OreBatch; " +
                $"id={supply.SelectedOreId}; failed={ex.GetType().Name}: {ex.Message}");
            // Do not mix vanilla ore into a batch already committed to another type.
            return false;
        }
        var previous = original;
        original = replacement!;
        placement = new(original.Pointer, supply.SelectedOreId!);
        DestroyUnusedStock(previous);
        return true;
    }

    private static void DestroyUnusedStock(GameItem item)
    {
        try { if (item.parentInventory == null) item.Destroy(); }
        catch (Exception ex) { SafeLog($"[WARN] [NicokoboForge/NpcStock] stock cleanup failed: {ex.Message}"); }
    }

    private static void StockPlacedPostfix(StockPlacement? __state)
    {
        if (__state == null) return;
        try
        {
            var items = EmporiumEntry.Instance?.frontInvinvElement?.childItems;
            bool accepted = false;
            if (items != null)
                for (int index = 0; index < items.Count; index++)
                    if (items[index]?.Pointer == __state.Pointer) { accepted = true; break; }
            SafeLog($"{(accepted ? "" : "[WARN] ")}[NicokoboForge/NpcStock] client=miner; mode=OreBatch; " +
                $"id={__state.ItemId}; status={(accepted ? "Applied" : "RejectedByNativePlacement")}");
        }
        catch (Exception ex) { SafeLog($"[WARN] [NicokoboForge/NpcStock] miner placement readback failed: {ex.Message}"); }
    }

    private static NativeItemDeclaration[] StockOffers(NpcTradeStockCategory categories, PlayerStore store)
    {
        var offers = NativeItemRegistry.Declarations().Where(candidate =>
            candidate.Options.NpcTrade is { } stock && (stock.Category & categories) != 0 &&
            NativeItemRegistry.IsAppliedItem(candidate.ItemId)).ToArray();
        var uniqueIds = offers.Where(candidate => candidate.Options.NpcTrade!.SkipWhenOwned)
            .Select(candidate => candidate.ItemId).ToHashSet(StringComparer.Ordinal);
        if (uniqueIds.Count == 0) return offers;
        // Supply can mutate inventories within this frame. Read live owned
        // items, including nested containers, instead of a cached count.
        var ownedIds = ForgeInventoryApi.CaptureRunItems(store)
            .Where(candidate => candidate.unitCount > 0 && uniqueIds.Contains(candidate.identifier) &&
                ForgeInventoryApi.IsPlayerOwned(candidate))
            .Select(candidate => candidate.identifier).ToHashSet(StringComparer.Ordinal);
        return offers.Where(candidate => !ownedIds.Contains(candidate.ItemId)).ToArray();
    }

    private static void AddStock(NpcTradeStockCategory categories)
    {
        if (!Installed) return;
        GameItem? item = null;
        bool submitted = false;
        string? itemId = null;
        try
        {
            var store = PlayerStore.instance;
            var client = store?.currentClientInstance?.storeClient;
            var inventory = EmporiumEntry.Instance?.frontInvinvElement;
            if (store == null || store.Pointer == IntPtr.Zero || client == null ||
                client.Pointer == IntPtr.Zero || inventory == null ||
                inventory.Pointer == IntPtr.Zero || inventory.childItems == null)
                throw new InvalidOperationException("NPC stock inventory or current supplier is unavailable");
            // A staged declaration cannot create native stock. Each supplier
            // draws one offer only from successfully applied factories.
            var offer = NativeNpcStockPolicy.Select(StockOffers(categories, store), categories, RNG.GetRandomDouble(0, 1));
            if (offer == null)
            {
                SafeLog($"[NicokoboForge/NpcStock] client={client.identifier}; categories={categories}; status=NoEligibleStock");
                return;
            }
            itemId = offer.ItemId;
            item = DirectoryMaster.Item(offer.ItemId, true);
            if (item == null || item.Pointer == IntPtr.Zero || item.identifier != offer.ItemId)
                throw new InvalidOperationException("Registered NPC stock could not be created");
            // Supply callbacks can clear or replace commissary stock. Wait for
            // their first placement, then reserve room ahead of vanilla goods.
            submitted = true;
            store.AddDirectSellingItemToTable(item, false, false, false, 0);
            bool accepted = false;
            for (int index = 0; index < inventory.childItems.Count; index++)
                if (inventory.childItems[index]?.Pointer == item.Pointer)
                { accepted = true; break; }
            SafeLog($"{(accepted ? "" : "[WARN] ")}[NicokoboForge/NpcStock] " +
                $"client={client.identifier}; categories={categories}; id={offer.ItemId}; " +
                $"status={(accepted ? "Applied" : "RejectedByNativePlacement")}");
        }
        catch (Exception ex)
        {
            if (!submitted && item != null && item.Pointer != IntPtr.Zero && item.parentInventory == null)
                try { item.Destroy(); } catch { }
            SafeLog($"[WARN] [NicokoboForge/NpcStock] categories={categories}; id={itemId}; failed={ex.GetType().Name}: {ex.Message}");
        }
    }
    private static void SafeLog(string message) { try { _log?.Invoke(message); } catch { } }
}
