using Nicokobo.Forge.Registration;
using Nicokobo.Forge.LogisticsExtension;
using Nicokobo.Forge.Logging;
using Nicokobo.Forge;

if (args is ["transfer-review"])
{
    try
    {
        ItemTransferChecks.Run();
        InventoryPlacementChecks.Run();
        LiquidTransferChecks.Run();
        WorkshopDeliveryChecks.Run();
    }
    catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
    return;
}

MachineCatalogChecks.Run();
ManufacturingTerminalChecks.Run();
ProductionValueChecks.Run();
RuntimeBoundaryChecks.Run();
ModuleInventoryChecks.Run();
AchievementChecks.Run();
WorkshopRewardChecks.Run();
WorkshopInputChecks.Run();
InventoryReadChecks.Run();
SpriteAtlasChecks.Run();
NightShopChecks.Run();
NpcStockChecks.Run();
ModuleActionChecks.Run();
SaveReadbackChecks.Run();
ItemTransferChecks.Run();
InventoryPlacementChecks.Run();
LiquidTransferChecks.Run();
WorkshopDeliveryChecks.Run();

static void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

foreach (var (name, expected) in new[]
{
    ("DEBUG", ModLogLevel.Debug), ("INFO", ModLogLevel.Info),
    ("WARN", ModLogLevel.Warn), ("ERROR", ModLogLevel.Error)
})
{
    Expect(ModLogLevels.TryParse(" " + name.ToLowerInvariant() + " ", out var parsed) &&
           parsed == expected, $"Log level {name} was not parsed");
    foreach (var messageLevel in Enum.GetValues<ModLogLevel>())
        Expect(ModLogLevels.Allows(parsed, messageLevel) == (messageLevel >= parsed),
            $"{name} incorrectly filtered {messageLevel}");
}
Expect(!ModLogLevels.TryParse("verbose", out var fallback) &&
       fallback == ModLogLevel.Warn, "Invalid log level did not fall back to WARN");

var catalog = new DeclarationCatalog();
var later = new RegistrationBatch("nicokobo.forge.example_two", "0.1.0")
    .Require("nicokobo.forge.example_one")
    .Define(DefinitionKind.Item, "other", "template=misc", "nicokobo.forge.example_one.item.starter")
    .Freeze();
Expect(catalog.Submit(later).Status == SubmitStatus.Accepted, "Out-of-order registration rejected");
Expect(catalog.Snapshot().Single().Status == RegistrationStatus.Waiting,
    "Unresolved dependency was marked ready");

var first = new RegistrationBatch("nicokobo.forge.example_one", "0.1.0")
    .Define(DefinitionKind.Item, "starter", "template=misc")
    .Define(DefinitionKind.Effect, "boost", "anyAction=boost", "nicokobo.forge.example_one.item.starter")
    .Freeze();
Expect(catalog.Submit(first).Status == SubmitStatus.Accepted, "First Mod rejected");
Expect(catalog.Snapshot().All(view => view.Status == RegistrationStatus.DependenciesResolved),
    "Cross-Mod or same-Mod dependency was not resolved");
Expect(catalog.Submit(first).Status == SubmitStatus.AlreadyPresent,
    "Identical re-registration was not idempotent");
var reordered = new RegistrationBatch("nicokobo.forge.example_one", "0.1.0")
    .Define(DefinitionKind.Effect, "boost", "anyAction=boost", "nicokobo.forge.example_one.item.starter")
    .Define(DefinitionKind.Item, "starter", "template=misc").Freeze();
Expect(catalog.Submit(reordered).Status == SubmitStatus.AlreadyPresent,
    "Definition order changed registration identity");

var changed = new RegistrationBatch("nicokobo.forge.example_one", "0.1.0")
    .Define(DefinitionKind.Item, "starter", "template=different").Freeze();
Expect(catalog.Submit(changed).Status == SubmitStatus.Conflict,
    "Changed content silently replaced existing declaration");

var duplicate = new RegistrationBatch("nicokobo.forge.duplicate", "0.1.0")
    .Define(DefinitionKind.Item, "same", "v1")
    .Define(DefinitionKind.Item, "same", "v2").Freeze();
Expect(catalog.Submit(duplicate).Status == SubmitStatus.Invalid,
    "Duplicate content ID was accepted");

var cycle = new DeclarationCatalog();
cycle.Submit(new RegistrationBatch("nicokobo.forge.alpha", "0.1.0")
    .Require("nicokobo.forge.beta").Define(DefinitionKind.Item, "a", "v1").Freeze());
cycle.Submit(new RegistrationBatch("nicokobo.forge.beta", "0.1.0")
    .Require("nicokobo.forge.alpha").Define(DefinitionKind.Item, "b", "v1").Freeze());
Expect(cycle.Snapshot().All(view => view.Status == RegistrationStatus.Waiting),
    "Dependency cycle was marked ready");

var versions = new DeclarationCatalog();
versions.Submit(new RegistrationBatch("nicokobo.forge.provider", "1.1.0")
    .Define(DefinitionKind.Item, "x", "v1").Freeze());
versions.Submit(new RegistrationBatch("nicokobo.forge.consumer", "0.1.0")
    .Require("nicokobo.forge.provider", "1.2.0")
    .Define(DefinitionKind.Item, "y", "v1").Freeze());
Expect(versions.Snapshot().Single(view => view.OwnerId == "nicokobo.forge.consumer").Status ==
       RegistrationStatus.Waiting, "Older dependency was marked resolved");

var nativeNodes = new NativeItemCatalog();
Func<int> neuralFactory = () => 1;
const string nativeNodeId = "nicokobo.aug.brain_computer_interface_module";
var repeatableStock = new NativeItemOptions(NightShopStockPolicy.Repeatable);
Expect(nativeNodes.Submit("nicokobo.aug", nativeNodeId, NativeItemKind.Node,
       neuralFactory, repeatableStock).Status ==
       SubmitStatus.Accepted, "Existing Aug native node ID was rejected");
Expect(nativeNodes.Submit("nicokobo.aug", nativeNodeId, NativeItemKind.Node,
       neuralFactory, repeatableStock).Status ==
       SubmitStatus.AlreadyPresent, "Identical node factory replay was not idempotent");
Expect(nativeNodes.Submit("nicokobo.aug", nativeNodeId, NativeItemKind.Node,
       neuralFactory, new NativeItemOptions(NightShopStockPolicy.Unique)).Status ==
       SubmitStatus.Conflict, "Night-shop policy changed under the same item ID");
Expect(nativeNodes.Submit("nicokobo.aug", nativeNodeId, NativeItemKind.Node,
       (Func<int>)(() => 2)).Status ==
       SubmitStatus.Conflict, "Different factory replaced the native node ID");
Expect(nativeNodes.Submit("nicokobo.forge.other", nativeNodeId, NativeItemKind.Node, neuralFactory).Status ==
       SubmitStatus.Invalid, "Foreign owner claimed the Aug native node ID");
Expect(nativeNodes.Submit("nicokobo.aug", nativeNodeId, NativeItemKind.Item,
       neuralFactory).Status == SubmitStatus.Conflict,
       "An ordinary item replaced a node with the same ID");
const string cardId = "nicokobo.logistics.memory_card";
Expect(nativeNodes.Submit("nicokobo.logistics", cardId, NativeItemKind.Item,
       neuralFactory).Status == SubmitStatus.Accepted,
       "Logistics memory card registration was rejected");
Expect(nativeNodes.Submit("nicokobo.logistics", cardId, NativeItemKind.Item,
       neuralFactory).Status == SubmitStatus.AlreadyPresent,
       "Logistics memory card factory replay was not idempotent");
const string moduleId = "nicokobo.forge.example_one.power_module";
Expect(nativeNodes.Submit("nicokobo.forge.example_one", moduleId, NativeItemKind.Module,
       neuralFactory).Status == SubmitStatus.Accepted,
       "Generic machine module registration was rejected");
Expect(nativeNodes.Submit("nicokobo.forge.example_one", moduleId, NativeItemKind.Node,
       neuralFactory).Status == SubmitStatus.Conflict,
       "Module ID was reused as a node");
const string amenityId = "nicokobo.aug.voice";
Expect(nativeNodes.Submit("nicokobo.aug", amenityId, NativeItemKind.Amenity,
       neuralFactory, new NativeItemOptions(NightShopStockPolicy.Unique)).Status ==
       SubmitStatus.Accepted, "Amenity night-shop registration was rejected");
Expect(nativeNodes.Snapshot().Count == 4, "Rejected native item changed the catalog");

var moduleBatch = new NativeItemCatalog();
var npcStock = new NativeItemCatalog();
foreach (var (id, category, weight) in new[]
{
    ("a_quartz", NpcTradeStockCategory.Ore, 0.5f),
    ("b_titanium", NpcTradeStockCategory.Ore, 0.5f),
    ("c_module", NpcTradeStockCategory.Module, 1f)
})
    Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock." + id, NativeItemKind.Item,
        neuralFactory, new() { NpcTrade = new(category, weight) }).Status == SubmitStatus.Accepted,
        "Valid NPC stock was rejected");
Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.final_cybernetic", NativeItemKind.Item,
    neuralFactory).Status == SubmitStatus.Accepted, "Manufacturing-only item registration failed");
var npcOffers = npcStock.Snapshot();
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Ore, 0) == null &&
    NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Ore, 0.499999) == null,
    "Native stock lost its interval in the mixed supply pool");
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Ore, 0.5)?.ItemId.EndsWith("a_quartz") == true,
    "Supply pool did not reach quartz after the native interval");
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Ore, 0.75)?.ItemId.EndsWith("b_titanium") == true,
    "Supply pool did not reach titanium at the next weighted interval");
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Ore, 0.999999)?.ItemId.EndsWith("b_titanium") == true,
    "Miner stock leaked a module or manufacturing-only item into the ore pool");
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NativeNpcStockPolicy.All, 0.75)?.ItemId.EndsWith("c_module") == true,
    "General supply did not include eligible modules");
Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NpcTradeStockCategory.Machine, 0.5) == null,
    "Empty supplier category produced stock");
foreach (var (sample, expectedId) in new[]
{
    (0d, "common_ore"), (0.699999, "common_ore"),
    (0.7, "nicokobo.stock.a_quartz"), (0.849999, "nicokobo.stock.a_quartz"),
    (0.85, "nicokobo.stock.b_titanium"), (0.999999, "nicokobo.stock.b_titanium")
})
    Expect(NativeNpcStockPolicy.SelectMinerOre(npcOffers, sample) == expectedId,
        "Miner batch draw omitted native ore, selected a non-ore item, or used the wrong weighted boundary");
Expect(NativeNpcStockPolicy.SelectMinerOre([], 0.999999) == "common_ore" &&
    NativeNpcStockPolicy.SelectMinerOre(npcOffers.Where(offer => offer.Options.NpcTrade?.Category == NpcTradeStockCategory.Module).ToArray(),
        0.999999) == "common_ore", "Miner without registered ore lost its native supply");
foreach (var (id, category) in new[] { ("drink", NpcTradeStockCategory.Food), ("gel", NpcTradeStockCategory.Medical) })
{
    var foodStock = new NativeItemCatalog();
    Expect(foodStock.Submit("nicokobo.food", "nicokobo.food." + id, NativeItemKind.Item,
        neuralFactory, new() { NpcTrade = new(category) }).Status == SubmitStatus.Accepted,
        "Food or medical NPC stock was rejected");
    var foodOffers = foodStock.Snapshot();
    Expect(NativeNpcStockPolicy.SelectFromPool(foodOffers, category, 0.5)?.ItemId == "nicokobo.food." + id &&
        NativeNpcStockPolicy.SelectFromPool(foodOffers, NativeNpcStockPolicy.All, 0.5)?.ItemId == "nicokobo.food." + id &&
        NativeNpcStockPolicy.SelectFromPool(foodOffers, NpcTradeStockCategory.Ore, 0.5) == null,
        "General supply omitted consumables or mineral supply selected them");
}
var cardStock = new NativeItemCatalog();
const string uniqueCardId = "nicokobo.forge.nico_card";
const string repeatableHouseholdId = "nicokobo.forge.stock_fixture";
Expect(cardStock.Submit("nicokobo.forge", uniqueCardId, NativeItemKind.Item, neuralFactory,
    new() { NpcTrade = new(NpcTradeStockCategory.Household) { SkipWhenOwned = true } }).Status == SubmitStatus.Accepted &&
    cardStock.Submit("nicokobo.forge", repeatableHouseholdId, NativeItemKind.Item, neuralFactory,
    new() { NpcTrade = new(NpcTradeStockCategory.Household) }).Status == SubmitStatus.Accepted,
    "Owned-stock policy registration failed");
var cardOffers = cardStock.Snapshot();
var ownedStockIds = new HashSet<string>(StringComparer.Ordinal) { uniqueCardId, repeatableHouseholdId };
foreach (double sample in new[] { 0.5, 0.999999 })
    Expect(NativeNpcStockPolicy.SelectFromPool(cardOffers, NpcTradeStockCategory.Household, sample, ownedStockIds)?.ItemId == repeatableHouseholdId,
        "Owned card occupied a weighted interval or owned repeatable stock was suppressed");
Expect(NativeNpcStockPolicy.SelectFromPool(cardOffers.Where(offer => offer.ItemId == uniqueCardId).ToArray(),
    NpcTradeStockCategory.Household, 0.5, ownedStockIds) == null,
    "An owned card remained eligible when it was the only offer");
ownedStockIds.Remove(uniqueCardId);
Expect(NativeNpcStockPolicy.SelectFromPool(cardOffers, NpcTradeStockCategory.Household, 0.5, ownedStockIds)?.ItemId == uniqueCardId,
    "Card stock did not become eligible after ownership ended");
foreach (double sample in new[] { -0.1, 1, double.NaN, double.PositiveInfinity })
    Expect(NativeNpcStockPolicy.SelectFromPool(npcOffers, NativeNpcStockPolicy.All, sample) == null &&
        NativeNpcStockPolicy.SelectMinerOre(npcOffers, sample) == null,
        "Invalid random sample selected NPC stock");
foreach (float weight in new[] { 0, -1, float.NaN, float.PositiveInfinity })
    Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.invalid", NativeItemKind.Item,
        neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Ore, weight) }).Status == SubmitStatus.Invalid,
        "Invalid NPC weight was staged");
Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.invalid", NativeItemKind.Item,
    neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Ore | NpcTradeStockCategory.Module) }).Status == SubmitStatus.Invalid,
    "One NPC item was allowed to claim multiple supply categories");
Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.invalid", NativeItemKind.Item,
    neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Material) { MinimumDay = -1 } }).Status == SubmitStatus.Invalid,
    "A negative minimum supply day was staged");
foreach (float weight in new[] { 0, -1, float.NaN, float.PositiveInfinity })
    Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.invalid", NativeItemKind.Item,
        neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Ore) { MinerWeight = weight } }).Status == SubmitStatus.Invalid,
        "Invalid miner weight was staged");
Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.invalid", NativeItemKind.Item,
    neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Food) { MinerWeight = 0.5f } }).Status == SubmitStatus.Invalid,
    "A non-ore item claimed a miner batch weight");
Expect(npcStock.Submit("nicokobo.stock", "nicokobo.stock.a_quartz", NativeItemKind.Item,
    neuralFactory, new() { NpcTrade = new(NpcTradeStockCategory.Ore, 1f) }).Status == SubmitStatus.Conflict,
    "NPC weight changed under a previously staged item ID");
var goodModules = new[]
{
    new NativeItemBatchEntry("nicokobo.matrix.first", NativeItemKind.Module,
        neuralFactory, repeatableStock),
    new NativeItemBatchEntry("nicokobo.matrix.second", NativeItemKind.Module,
        neuralFactory, repeatableStock)
};
Expect(moduleBatch.SubmitBatch("nicokobo.matrix", goodModules).Status ==
       SubmitStatus.Accepted && moduleBatch.Snapshot().Count == 2,
    "Valid module batch was not staged together");
var conflictingModules = new[]
{
    new NativeItemBatchEntry("nicokobo.matrix.third", NativeItemKind.Module,
        neuralFactory, repeatableStock),
    new NativeItemBatchEntry("nicokobo.matrix.second", NativeItemKind.Module,
        (Func<int>)(() => 2), repeatableStock)
};
Expect(moduleBatch.SubmitBatch("nicokobo.matrix", conflictingModules).Status ==
       SubmitStatus.Conflict && moduleBatch.Snapshot().Count == 2,
    "Conflicting module batch left an earlier ID staged");

var effects = new NativeEffectCatalog();
Func<int> overclockFactory = () => 25;
const string overclockId = "nicokobo.aug.effect.overclock";
Expect(effects.Submit("nicokobo.aug", overclockId, false,
       overclockFactory).Status == SubmitStatus.Accepted,
       "Content-owned module effect was rejected");
Expect(effects.Submit("nicokobo.aug", overclockId, false,
       overclockFactory).Status == SubmitStatus.AlreadyPresent,
       "Same module effect replay was not idempotent");
Expect(effects.Submit("nicokobo.aug", overclockId, true,
       overclockFactory).Status == SubmitStatus.Conflict,
       "Random-pool policy changed under the same effect ID");
Expect(effects.Submit("nicokobo.forge.other", overclockId, false,
       overclockFactory).Status == SubmitStatus.Invalid,
       "Foreign Mod claimed another owner's effect ID");
Expect(effects.Snapshot().Count == 1,
       "Rejected effect modified the catalog");

var effectBatch = new NativeEffectCatalog();
var goodEffects = new[]
{
    new NativeEffectBatchEntry("nicokobo.matrix.effect.first", true,
        overclockFactory),
    new NativeEffectBatchEntry("nicokobo.matrix.effect.second", true,
        overclockFactory)
};
Expect(effectBatch.SubmitBatch("nicokobo.matrix", goodEffects).Status ==
       SubmitStatus.Accepted && effectBatch.Snapshot().Count == 2,
    "Valid effect batch was not staged together");
var conflictingEffects = new[]
{
    new NativeEffectBatchEntry("nicokobo.matrix.effect.third", true,
        overclockFactory),
    new NativeEffectBatchEntry("nicokobo.matrix.effect.second", false,
        overclockFactory)
};
Expect(effectBatch.SubmitBatch("nicokobo.matrix", conflictingEffects).Status ==
       SubmitStatus.Conflict && effectBatch.Snapshot().Count == 2,
    "Conflicting effect batch left an earlier ID staged");

var starts = new StartCatalog();
const string augStartId = "nicokobo.aug.mechanical_ascension.start";
Expect(starts.Submit("nicokobo.aug", augStartId, 55).Status ==
       SubmitStatus.Accepted, "Aug start identity was rejected");
Expect(starts.Submit("nicokobo.aug", augStartId, 55).Status ==
       SubmitStatus.AlreadyPresent, "Identical start claim was not idempotent");
Expect(starts.Submit("nicokobo.forge.other", "nicokobo.forge.other.start", 55).Status ==
       SubmitStatus.Conflict, "A second start claimed native type 55");
Expect(starts.Submit("nicokobo.forge.other", augStartId, 56).Status ==
       SubmitStatus.Invalid, "Foreign owner claimed the Aug start ID");
Expect(starts.Snapshot().Count == 1, "Rejected start changed the catalog");

var emptyCard = LogisticsRules.NewCard();
var ore = new LogisticsItemFacts("ore", "material", new[] { "hot" });
Expect(emptyCard.Evaluate(ore, _ => true).Decision == LogisticsRuleDecision.Denied,
    "New empty card unexpectedly allowed all items");
var filter = new LogisticsRules(true, true,
    new[] { new LogisticsCondition(LogisticsConditionKind.Tag, "hot") },
    new[] { new LogisticsCondition(LogisticsConditionKind.Type, "material") });
Expect(filter.Evaluate(ore, _ => true).Decision == LogisticsRuleDecision.Denied,
    "Blacklist did not take precedence over type whitelist");
Expect(filter.Evaluate(ore with { ActualTags = Array.Empty<string>() }, _ => true).Decision ==
       LogisticsRuleDecision.Allowed, "Actual instance tag was not used");
Expect(filter.Evaluate(ore, c => c.Key != "hot").Decision == LogisticsRuleDecision.Invalid,
    "Unknown saved tag silently widened the rule");
var card = new LogisticsCard("instance-1", "矿石", 3, filter);
Expect(card.Edit(3, "矿石二", filter).Revision == 4,
    "Memory card edit did not advance revision");
try { card.Edit(2, "stale", filter); throw new Exception("Stale card edit accepted"); }
catch (InvalidOperationException) { }
var export = filter.Export(card.Name);
Expect(export.Contains("\"version\": 1") && !export.Contains(card.CardId),
    "Rule export leaked card identity or lacks version");

var cards = new Dictionary<string, LogisticsCard> { [card.CardId] = card };
var networks = new Dictionary<string, LogisticsNetwork>
{
    ["main"] = new("main", "Main", true, null),
    ["sub"] = new("sub", "Materials", false, card.CardId)
};
Expect(LogisticsRouting.ResolvePush(new(null, null), false, "main", cards, networks).Status ==
       LogisticsRouteStatus.MissingCard, "Unbound push node was activated");
Expect(LogisticsRouting.ResolvePush(new(card.CardId, null), false, "main", cards, networks)
       .Endpoint == LogisticsEndpointKind.NearbyContainers,
       "Pre-unlock push did not use nearby containers");
Expect(LogisticsRouting.ResolvePush(new(card.CardId, null), true, "main", cards, networks)
       .NetworkId == "main", "Unbound node did not select main network");
Expect(LogisticsRouting.ResolvePull(new(null, "deleted"), true, "main", networks).Status ==
       LogisticsRouteStatus.MissingNetwork, "Deleted explicit network fell back to main");
Expect(LogisticsRouting.CanDeposit(networks["sub"], ore, cards, _ => true).Decision ==
       LogisticsRuleDecision.Denied, "Subnetwork did not apply its card");
Expect(LogisticsRouting.CanDeposit(networks["main"], ore, cards, _ => true).Decision ==
       LogisticsRuleDecision.Allowed, "Main network applied a rule");
Expect(LogisticsRouting.MissingAmount(10, 6) == 4 &&
       LogisticsRouting.MissingAmount(10, 12) == 0,
       "Push target quantity counted stock incorrectly");
var runId = Guid.NewGuid().ToString("D");
var config = new LogisticsConfig(runId, 2, 1, true,
    new[] { card },
    new[] { new LogisticsNodeConfig("push-instance", true,
        new LogisticsNodeBinding(card.CardId, "sub")) },
    networks.Values.ToArray());
var encoded = LogisticsConfigCodec.Write(config);
Expect(LogisticsConfigCodec.Read(encoded, runId, 2) is
       { Status: LogisticsConfigReadStatus.Ready, Config: { } restored } &&
       restored.Cards[0].Rules.Evaluate(ore, _ => true).Decision ==
           LogisticsRuleDecision.Denied,
       "Logistics card and network configuration did not round-trip");
Expect(LogisticsConfigCodec.Read(encoded, Guid.NewGuid().ToString("D"), 2).Status ==
       LogisticsConfigReadStatus.IdentityMismatch,
       "Logistics configuration leaked across runs");
Expect(LogisticsConfigCodec.Read(encoded.Replace("\"version\":1", "\"version\":2"),
       runId, 2).Status == LogisticsConfigReadStatus.FutureVersion,
       "Future logistics schema was accepted");

Expect(MachineLiquidMath.ToParts(100) == 100_000, "100 ml conversion");
try { MachineLiquidMath.ToParts(int.MaxValue); throw new Exception("Overflowing ml accepted"); }
catch (ArgumentOutOfRangeException) { }

Console.WriteLine("Nicokobo Forge registration, logistics and liquid domain checks passed.");
