using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine;

namespace Nicokobo.Forge.Workshop;

// Built-in provider for native gameplay goals. It owns no content Mod upgrades.
internal static class NativeWorkshop
{
    internal const string OwnerId = "nicokobo.forge";
    internal const string ChainId = "nicokobo.forge.native_workshop";
    private static readonly LocalizedItemText CardDescription = new(
        "双击名片或按 N 键打开 Nico工坊，查看成就并领取奖励。",
        "Double-click or press N to open Nico Workshop, view achievements and collect rewards.");
    private static readonly LocalizedItemText CardFlavor = new(
        "有些成绩，总该留下点凭据。",
        "Some achievements deserve a little proof.");
    private static readonly List<IDisposable> Leases = [];
    private static readonly ForgeWorkshopGroup[] Groups =
    [new("产业成果", "Industry", 4), new("收藏与装备", "Collection & Gear", 2),
     new("经营与声望", "Business & Reputation", 3), new("原版通关", "Native Victory", 1)];
    private static PlayerStore? _store;
    private static AchievementState? _state, _confirmed;
    private static string? _json, _confirmedJson;
    private static Action<string>? _log;
    private static bool _enabled, _bindRequested, _busy, _saving, _dirty, _wasVisible, _blocked, _observing;
    private static DateTime _nextUpdate, _nextCard, _nextSave;
    private static string _ending = "", _message = "", _lastFailure = "";
    private static Dictionary<string, AchievementEvidence> _evidence = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Unavailable = new(StringComparer.Ordinal);
    private static readonly DragIdleGate Idle = new();

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) { log("[WARN] [NicokoboForge/Workshop] built-in achievements disabled by native capability gates"); return; }
        try { WorkshopRewardDelivery.RequireContracts(); }
        catch (Exception ex)
        {
            log("[WARN] [NicokoboForge/Workshop] built-in achievements disabled by reward shape contracts: " + ex.Message);
            return;
        }
        const string hookId = "nicokobo.forge.native_workshop";
        var hooks = new NativeHook[]
        {
            new(typeof(PlayerStore), nameof(PlayerStore.StartNewGame), [], typeof(void), typeof(NativeWorkshop), Prefix: nameof(ResetPrefix)),
            new(typeof(PlayerStore), nameof(PlayerStore.InitialSave), [], typeof(void), typeof(NativeWorkshop), Postfix: nameof(InitialPostfix)),
            new(typeof(PlayerStore), nameof(PlayerStore.SaveGame), [], typeof(void), typeof(NativeWorkshop), Prefix: nameof(SavePrefix), Postfix: nameof(SavePostfix)),
            new(typeof(PlayerStore), nameof(PlayerStore.ExecuteGameOver), [typeof(string)], typeof(void), typeof(NativeWorkshop), Postfix: nameof(EndingPostfix)),
            new(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction", [typeof(GameItem), typeof(Vector2)], typeof(void), typeof(NativeWorkshop), Prefix: nameof(CardDoubleClick))
        };
        if (!NativeHookSet.Install(hookId, hooks, log)) return;
        try
        {
            Leases.Add(ForgeLifecycleApi.Subscribe(OwnerId, OwnerId + ".workshop.after_load", ForgeLifecyclePhase.AfterLoad,
                _ => { Reset(); _bindRequested = true; }));
            Leases.Add(ForgeLifecycleApi.Subscribe(OwnerId, OwnerId + ".workshop.after_night", ForgeLifecyclePhase.AfterNight, _ => _dirty = true));
            Leases.Add(ForgeLifecycleApi.Subscribe(OwnerId, OwnerId + ".workshop.after_day", ForgeLifecyclePhase.AfterEndDay, _ => _dirty = true));
            ForgePresentationApi.RegisterOwnerDisplayName(OwnerId, "Nico工坊", "Nico Workshop");
            NicoCardIconRuntime.Configure(log, line => log("[WARN] " + line));
            var card = ForgeItemApi.RegisterItem(OwnerId, AchievementRules.CardId, CreateCard, new NativeItemOptions(
                Name: new("Nico工坊名片", "Nico Workshop Card"),
                ShortDescription: CardDescription,
                FlavorText: CardFlavor)
            {
                NpcTrade = new(NpcTradeStockCategory.Household) { SkipWhenOwned = true }
            });
            if (card.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)) throw new InvalidOperationException(card.Reason);
            if (card.Status == SubmitStatus.Accepted)
                LootRegistry.AddEntry(OwnerId, TableMaster.householdTable,
                    AchievementRules.CardId, ForgeNumbers.Achievements.CardLootWeight);
            var tab = ForgeWorkshopApi.RegisterChain(OwnerId, ChainId, "原版", "Base Game", Snapshot, Claim);
            if (tab.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent)) throw new InvalidOperationException(tab.Reason);
            _enabled = true;
            NativeVictoryBudget.Install(log);
            ForgeWorkshopApi.SetDefaultChain(ChainId);
            InstallObservationHooks(log);
            log("[INFO] [NicokoboForge/Workshop] native achievements registered; nodes=10; card=all starts; completion!=claim");
        }
        catch (Exception ex)
        {
            foreach (var lease in Leases) lease.Dispose(); Leases.Clear();
            NativeHookSet.Remove(hookId, log);
            Warn("Built-in provider unavailable: " + ex.Message);
        }
    }

    private static void InstallObservationHooks(Action<string> log)
    {
        // Cheap dirty flags coalesce frequent native mutations. Ownership targets are also
        // sampled before removal/tag changes, so spending a completed proof does not undo it.
        var hooks = new List<NativeHook>
        {
            new(typeof(GameItem), nameof(GameItem.ModifyTag), [typeof(string), typeof(Il2CppSystem.Action<TagState>), typeof(bool)], typeof(GameItem),
                typeof(NativeWorkshop), Prefix: nameof(BeforeItemMutation), Postfix: nameof(AfterItemMutation)),
            new(typeof(GameItem), nameof(GameItem.SetUnitCount), [typeof(int)], typeof(GameItem), typeof(NativeWorkshop),
                Prefix: nameof(BeforeItemMutation), Postfix: nameof(AfterItemMutation))
        };
        // GameInventory declares these methods abstract in the native game. Interop
        // emits callable virtual wrappers, but their native detour address is zero.
        // Observe each concrete implementation, including slot and scrolling bags.
        foreach (var inventoryType in new[] { typeof(GameGridInventory), typeof(GameSlotInventory),
                     typeof(GameGridScrollableInventory), typeof(GameCharacterRaidInventory) })
            foreach (string name in new[] { nameof(GameInventory.UncheckedAccept), nameof(GameInventory.Expel) })
                hooks.Add(new(inventoryType, name, [typeof(GameItem)], typeof(bool), typeof(NativeWorkshop),
                    Prefix: name == nameof(GameInventory.Expel) ? nameof(BeforeRemoval) : null, Postfix: nameof(MarkDirty)));
        if (!NativeHookSet.Install("nicokobo.forge.native_workshop.observation", hooks, log))
            Warn("Mutation observation unavailable; goals still refresh on opening, saving and day boundaries");
    }

    internal static void Update()
    {
        if (!_enabled) return;
        NicoCardIconRuntime.TryInstall();
        bool visible = ForgeWorkshopApi.IsVisible;
        if (visible && !_wasVisible) _dirty = true;
        _wasVisible = visible;
        var now = DateTime.UtcNow;
        if (!Idle.Ready(ForgeInventoryApi.IsItemDragActive, now) || _busy || now < _nextUpdate) return;
        _nextUpdate = now.AddMilliseconds(ForgeNumbers.Achievements.UpdateMilliseconds);
        try
        {
            if (_bindRequested) Bind();
            if (!Current()) return;
            if (_dirty) Observe();
            if (!_blocked && DateTime.UtcNow >= _nextSave)
            {
                _nextSave = DateTime.UtcNow.AddSeconds(ForgeNumbers.Achievements.SaveRetrySeconds);
                if (_json != _confirmedJson) Save();
                if (_state!.Pending != null) Recover();
            }
            if (!_blocked && !_state!.CardGranted && _state.Pending == null && DateTime.UtcNow >= _nextCard)
            {
                _nextCard = DateTime.UtcNow.AddSeconds(ForgeNumbers.Achievements.CardRetrySeconds);
                Deliver(AchievementRules.CardNode, [new(AchievementRules.CardId, "Nico工坊名片", "Nico Workshop Card")]);
            }
        }
        catch (Exception ex) { Warn("Update deferred: " + ex.Message); }
    }

    private static void Bind()
    {
        var store = PlayerStore.Instance;
        var inventory = EmporiumEntry.Instance?.invElement;
        if (store == null || store.Pointer == IntPtr.Zero || inventory == null || inventory.Pointer == IntPtr.Zero ||
            string.IsNullOrWhiteSpace(store.runID) || store.saveSlotId < 0 || store.modData == null) return;
        _store = store; _bindRequested = false; _blocked = false;
        var read = ForgeRunDataApi.Read(store, OwnerId, AchievementRules.SaveKey);
        if (read.Status == RunDataStatus.Missing)
        {
            _state = AchievementState.Empty(store.runID, store.saveSlotId); _json = null; _confirmed = null; _confirmedJson = null;
        }
        else if (read.Status == RunDataStatus.Present)
        {
            try { _state = AchievementRules.Decode(read.Json!, store.runID, store.saveSlotId); _json = read.Json; }
            catch (Exception ex) { Block("成就记录无法读取：" + ex.Message); return; }
            Confirm();
        }
        else { Block("成就记录适配不可用：" + read.Reason); return; }
        // Refresh this provider's own cards. A granted card lost later is not replaced.
        foreach (var item in ForgeInventoryApi.CaptureRunItems(store))
            if (item.identifier == AchievementRules.CardId && ForgeInventoryApi.IsPlayerOwned(item)) RefreshCard(item);
        _dirty = true; _nextCard = DateTime.UtcNow;
        Observe();
        if (_json == null) Stage(_state!);
        if (_json != _confirmedJson) Save();
        if (_state?.Pending != null) Recover();
    }

    private static bool Current() => _store != null && _state != null && PlayerStore.Instance?.Pointer == _store.Pointer &&
        _store.runID == _state.RunId && _store.saveSlotId == _state.SlotId;

    private static void ResetPrefix() => Reset();
    private static void InitialPostfix() => _bindRequested = true;
    private static void Reset()
    {
        ForgeWorkshopApi.ResetRunSelection();
        _store = null; _state = null; _confirmed = null; _json = null; _confirmedJson = null;
        _bindRequested = false; _busy = false; _saving = false; _blocked = false; _dirty = true; _wasVisible = false;
        Idle.Reset();
        NativeVictoryBudget.Reset();
        _ending = ""; _message = ""; _lastFailure = ""; Unavailable.Clear(); _evidence.Clear(); _nextUpdate = DateTime.MinValue; _nextSave = DateTime.MinValue;
    }

    private static void Observe()
    {
        if (!Current() || _blocked || _busy || _observing) return;
        _observing = true;
        try
        {
            _dirty = false; Unavailable.Clear();
            _evidence = AchievementRules.Evaluate(NativeAchievementAdapter.Capture(_store!, _ending, Unavailable), English());
            var next = AchievementRules.Latch(_state!, _evidence.Where(x => !Unavailable.ContainsKey(x.Key)).ToDictionary(x => x.Key, x => x.Value));
            if (next.Completed.Count != _state!.Completed.Count) Stage(next);
        }
        finally { _observing = false; }
    }

    private static void ObserveItem(GameItem? item)
    {
        if (!Current() || _blocked || _busy || _observing || item == null || item.Pointer == IntPtr.Zero || !NativeAchievementAdapter.Relevant(item.identifier)) return;
        _observing = true;
        try
        {
            var snapshot = NativeAchievementAdapter.Capture(item);
            if (!snapshot.Owned) return;
            var proofs = AchievementRules.Evaluate(new([snapshot], 0, 0, 0), English());
            var next = AchievementRules.Latch(_state!, proofs);
            if (next.Completed.Count != _state!.Completed.Count) Stage(next);
        }
        catch (Exception ex) { Warn("Item evidence unavailable: " + ex.Message); }
        finally { _observing = false; }
    }
    private static void BeforeItemMutation(GameItem __instance) => ObserveItem(__instance);
    private static void AfterItemMutation(GameItem __instance) { ObserveItem(__instance); MarkDirty(); }
    private static void BeforeRemoval(GameItem __0)
    {
        if (_busy || !Current() || __0 == null || !NativeAchievementAdapter.Relevant(__0.identifier)) return;
        // All tools must coexist in one snapshot; never union their historical observations.
        if (!_state!.Completed.ContainsKey("scavenger_toolset") && AchievementRules.Tools.Contains(__0.identifier)) ObserveTools();
        else ObserveItem(__0);
    }
    private static void ObserveTools()
    {
        if (!Current() || _blocked || _busy || _observing) return;
        _observing = true;
        try
        {
            var proof = AchievementRules.Evaluate(new(NativeAchievementAdapter.CaptureTools(_store!), 0, 0, 0), English())["scavenger_toolset"];
            var next = AchievementRules.Latch(_state!, new Dictionary<string, AchievementEvidence> { ["scavenger_toolset"] = proof });
            if (next.Completed.Count != _state!.Completed.Count) Stage(next);
        }
        catch (Exception ex) { Warn("Tool evidence unavailable: " + ex.Message); }
        finally { _observing = false; }
    }
    private static void MarkDirty() { if (!_busy && !_observing) _dirty = true; }

    private static void EndingPostfix(PlayerStore __instance, string __0)
    {
        if (!Current() || _store!.Pointer != __instance.Pointer || !AchievementRules.IsVictory(__0)) return;
        _ending = __0;
        try { Observe(); Save(); }
        catch (Exception ex) { Warn("Victory record pending: " + ex.Message); }
    }

    private static bool Stage(AchievementState next)
    {
        if (_blocked || _store == null) return false;
        string json = AchievementRules.Encode(next);
        if (json == _json) { _state = next; return true; }
        var staged = ForgeRunDataApi.Stage(_store, OwnerId, AchievementRules.SaveKey, next.RunId, next.SlotId, _json, json);
        if (staged.Status != RunDataStatus.StagedInMemory) { Block("成就记录发生冲突，暂不可领取。" + staged.Reason); return false; }
        _state = next; _json = json; return true;
    }

    private static bool SavePrefix(PlayerStore __instance)
    {
        // Native callbacks cannot save an intermediate delivery. The deliberate save follows all placements.
        if (_busy && !_saving && _store?.Pointer == __instance.Pointer) return false;
        if (_saving || !Current() || _store!.Pointer != __instance.Pointer || _blocked) return true;
        try { Observe(); }
        catch (Exception ex) { Warn("Save evidence deferred: " + ex.Message); }
        return true;
    }
    private static void SavePostfix(PlayerStore __instance)
    {
        if (!Current() || _store!.Pointer != __instance.Pointer || _json == null) return;
        Confirm();
    }
    private static bool Save()
    {
        if (!Current() || _blocked || _json == null) return false;
        bool prior = _saving; _saving = true;
        try { _store!.SaveGame(); return Confirm(); }
        catch (Exception ex) { Warn("Save confirmation deferred: " + ex.Message); return Confirm(); }
        finally { _saving = prior; }
    }
    private static bool Confirm()
    {
        if (_store == null || _state == null || _json == null) return false;
        var check = Readback(_json, _state.Pending);
        if (!check.Matched) { Warn("Save readback pending: " + check.Reason); return false; }
        _confirmedJson = _json; _confirmed = AchievementRules.Decode(_json, _state.RunId, _state.SlotId);
        NativeVictoryBudget.SetActive(!_blocked && AchievementRules.HasVictoryBudgetReward(_confirmed),
            _store.currentClientInstance?.GetClientBlueprint());
        _lastFailure = ""; return true;
    }
    private static AchievementSaveResult Readback(string json, PendingReward? pending = null) =>
        AchievementSaveReadback.Compare(Path.Combine(Application.persistentDataPath, $"save_{_state!.SlotId}.es3"),
            _state.RunId, _state.SlotId, json, pending);

    private static void Recover()
    {
        if (!Current() || _blocked || _state!.Pending is not { } pending) return;
        if (_json != _confirmedJson && !Confirm()) return;
        // Resume only the saved delivery. Never reconstruct and resend its reward manifest.
        if (!Readback(_json!, pending).Matched)
        {
            _message = English() ? "Reward save confirmation pending; no duplicate delivery." : "奖励等待保存确认，不会重复发放。";
            return;
        }
        if (Stage(AchievementRules.Claim(_state)))
        {
            bool confirmed = Save();
            _message = confirmed ? "" : (English() ? "Claim confirmation pending." : "领取记录等待保存确认。");
        }
    }

    private static void Claim(string id)
    {
        if (id == AchievementRules.VictoryNode && !NativeVictoryBudget.Installed) return;
        if (!Current() || _blocked || _busy || _state!.Pending != null || _json != _confirmedJson ||
            _confirmed?.Completed.ContainsKey(id) != true || _state.Claimed.Contains(id)) return;
        var definition = AchievementRules.Definitions.FirstOrDefault(x => x.Id == id);
        if (definition != null) Deliver(id, definition.Rewards);
    }

    private static void Deliver(string nodeId, AchievementReward[] rewards)
    {
        if (!Current() || _busy || _blocked || _state!.Pending != null || _json != _confirmedJson) return;
        var inventory = EmporiumEntry.Instance?.invElement;
        if (inventory == null || inventory.Pointer == IntPtr.Zero) return;
        var products = new List<GameItem>();
        RewardReceipt[] receipts = [];
        var before = _state;
        bool placementAttempted = false, journaled = false;
        _busy = true;
        try
        {
            foreach (var reward in rewards)
                for (int i = 0; i < reward.Count; i++)
                {
                    var product = DirectoryMaster.Item(reward.ItemId, true) ?? throw new InvalidOperationException("Reward factory unavailable: " + reward.ItemId);
                    if (product.Pointer == IntPtr.Zero || product.parentInventory != null || products.Any(x => x.Pointer == product.Pointer))
                        throw new InvalidOperationException("Reward factory contract mismatch: " + reward.ItemId);
                    products.Add(product);
                    if (product.identifier != reward.ItemId || product.unitCount != 1 || product.GetUniqueID() <= 0 ||
                        products.Take(products.Count - 1).Any(x => x.uniqueId == product.uniqueId))
                        throw new InvalidOperationException("Reward factory contract mismatch: " + reward.ItemId);
                }
            receipts = products.Select(x => new RewardReceipt(x.identifier, x.uniqueId, x.unitCount)).ToArray();
            var plan = WorkshopRewardDelivery.Plan(inventory, products);
            if (plan == null)
            {
                _message = English() ? "Make room in the main inventory for the whole reward." : "主背包空间不足，请为整份奖励腾出空间。";
                return;
            }
            placementAttempted = true;
            if (!WorkshopRewardDelivery.Apply(inventory, plan)) throw new InvalidOperationException("Reward placement readback failed");
            var pending = new PendingReward(nodeId, receipts);
            if (!Stage(before with { Pending = pending })) throw new InvalidOperationException("Delivery journal could not be staged");
            journaled = true;
            // Native SaveGame serializes these resources and this pending manifest together.
            if (!Save()) { _message = English() ? "Delivered; awaiting save confirmation." : "奖励已交付，等待保存确认。"; return; }
            if (!Stage(AchievementRules.Claim(_state!))) return;
            bool confirmed = Save();
            _message = confirmed ? "" : (English() ? "Claim confirmation pending." : "领取记录等待保存确认。");
            _dirty = true;
            Log($"reward={nodeId}; items={products.Count}; slotReadback={confirmed}");
        }
        catch (Exception ex)
        {
            _message = English() ? "Reward unavailable. Check the Forge log before retrying." : "奖励暂不可领取，请检查 Forge 日志后重试。";
            Warn("Delivery deferred: " + ex.Message);
        }
        finally
        {
            if (!journaled && !WorkshopRewardDelivery.Recall(products))
            {
                // Retain exact identities if native rollback is incomplete. Quarantine rather than resend.
                if (placementAttempted)
                {
                    Stage(before with { Pending = new(nodeId, receipts) });
                    Block("奖励交付中断，已保留原事务，暂不可再次领取。");
                }
            }
            _busy = false;
        }
    }

    private static bool CardDoubleClick(GameItem __0)
    {
        if (!_enabled || __0 == null || __0.identifier != AchievementRules.CardId || !ForgeInventoryApi.IsPlayerOwned(__0)) return true;
        return !ForgeWorkshopApi.OpenChain(ChainId);
    }
    private static GameItem CreateCard()
    {
        var item = DirectoryMaster.Item("joe_card", true) ?? throw new InvalidOperationException("Native card template unavailable");
        if (!NicoCardIconRuntime.MatchesShape(item)) throw new InvalidOperationException("Native card shape must be 2x1");
        item.identifier = AchievementRules.CardId; item.identifierName = AchievementRules.CardId;
        RefreshCard(item);
        return item;
    }
    private static void RefreshCard(GameItem item)
    {
        item.name = English() ? "Nico Workshop Card" : "Nico工坊名片";
        item.shortDescription = CardDescription.For(English());
        item.flavorText = CardFlavor.For(English());
        item.mayStackItemFunc = null;
        if (!item.IsTag("NOT_FOR_RESALE") && !item.EnableTag("NOT_FOR_RESALE", false)) throw new InvalidOperationException("Card resale protection failed");
        if (NicoCardIconRuntime.TryInstall()) NicoCardIconRuntime.Assign(item);
        else { item.spriteAtlasPath = NicoCardIconRuntime.AtlasKey; item.spritePath = NicoCardIconRuntime.SpriteKey; }
    }

    private static ForgeWorkshopSnapshot? Snapshot()
    {
        if (!_enabled || _store == null || PlayerStore.Instance?.Pointer != _store.Pointer || string.IsNullOrWhiteSpace(_store.runID)) return null;
        bool english = English();
        if (_dirty && !_busy && !ForgeWorkshopApi.IsVisible && Idle.Ready(ForgeInventoryApi.IsItemDragActive, DateTime.UtcNow))
            try { Observe(); } catch (Exception ex) { Warn("Snapshot evidence deferred: " + ex.Message); }
        var entries = AchievementRules.Definitions.Select(definition =>
        {
            bool completed = _confirmed?.Completed.ContainsKey(definition.Id) == true;
            bool claimed = _confirmed?.Claimed.Contains(definition.Id) == true;
            bool pending = _state?.Pending != null || _json != _confirmedJson;
            bool rewardAvailable = definition.Id != AchievementRules.VictoryNode || NativeVictoryBudget.Installed;
            string progress = _evidence.GetValueOrDefault(definition.Id)?.Progress ?? (english ? "Checking native state" : "读取原生状态");
            string state = claimed ? (english ? "Claimed" : "已领取") : completed ? (english ? "Completed · unclaimed" : "已达成 · 未领取") : (english ? "In progress" : "未达成");
            if (definition.Id == AchievementRules.VictoryNode && claimed)
                state = english ? "Victory badge awarded · NPC purchase budgets x2" : "通关纪念徽章已领取 · NPC收购预算×2";
            if (_blocked) state = english ? "Achievement record unavailable" : "成就记录不可用";
            else if (!rewardAvailable) state = english ? "Purchase budget reward unavailable" : "收购预算奖励暂不可用";
            else if (pending) state += english ? " · save pending" : " · 等待保存";
            else if (!completed && Unavailable.ContainsKey(definition.Id)) state = english ? "Native progress temporarily unavailable" : "原生进度暂不可读取";
            string cost = (english ? "Progress: " : "当前进度：") + progress;
            if (definition.Id == AchievementRules.VictoryNode) cost += english
                ? "\nReward: Victory commemorative badge + all NPC purchase budgets x2\nActive after claiming for this run; no inventory space"
                : "\n奖励：通关纪念徽章＋所有NPC收购预算×2\n领取后本周目持续生效，不占库存";
            return new ForgeWorkshopEntry(definition.Id, english ? definition.English : definition.Chinese,
                definition.Id == "workshop_master" ? (english ? "Native victory" : "原版通关") : (english ? "Native achievement" : "原版成就"),
                english ? definition.ConditionEnglish : definition.ConditionChinese,
                english ? "Independent goal" : "独立判定", cost, completed ? (english ? "CLAIM" : "可领奖") : progress,
                state, claimed ? (english ? "Claimed" : "已领取") : (english ? "Claim reward" : "领取奖励"),
                claimed, true, completed && !claimed && !pending && !_blocked && rewardAvailable && _state?.Claimed.Contains(definition.Id) != true)
            {
                Completed = completed,
                Rewards = definition.Rewards.Select(x => new ForgeWorkshopReward(ForgeWorkshopResourceKind.Item, x.ItemId, x.Count) { DisplayName = english ? x.English : x.Chinese }).ToArray(),
                VisualLinks = definition.Id == "workshop_master" ? [] : ["workshop_master"],
                GraphX = definition.X, GraphY = definition.Y
            };
        }).ToArray();
        return new(_store.playerCash, english, Groups, entries, _message);
    }
    private static bool English() => ForgePresentationApi.PreferEnglish();
    private static void Block(string reason)
    {
        _blocked = true; NativeVictoryBudget.SetActive(false, null); _message = reason; Warn(reason);
    }
    private static void Log(string line) { try { _log?.Invoke("[INFO] [NicokoboForge/Workshop] " + line); } catch { } }
    private static void Warn(string line)
    {
        if (_lastFailure == line) return;
        _lastFailure = line;
        try { _log?.Invoke("[WARN] [NicokoboForge/Workshop] " + line); } catch { }
    }
}
