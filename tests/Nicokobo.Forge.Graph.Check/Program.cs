using System.Text.Json.Nodes;
using System.Text.Json;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge;

int checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
void Reject(Action action, string reason) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, reason); }
var store = new PlayerStore(); PlayerStore.instance = store;
var bag = new GameItem { uniqueId = 10 };
var child = new GameItem { uniqueId = 11, identifier = "large_purple_injector" }; bag.Nested.Add(child);
child.AddItemFeature(new() { identifier = "genuine", actualValue = 160, isDisabled = false });
string graph = ForgeSaveGraphApi.Encode(bag);
Check(store.savedItemFeatureList == null, "First-save encoding restores the originally null feature table");
var decoded = ForgeSaveGraphApi.Decode(graph);
Check(decoded.uniqueId == 10 && decoded.Nested.Single().uniqueId == 11, "Container graph preserves root and child IDs");
var feature = decoded.Nested.Single().itemFeatures.Single();
Check(feature.identifier == "genuine" && feature.actualValue == 160 && !feature.isDisabled && feature.parentItemUniqueId == 11,
    "Detached decode preserves native feature condition, value, enablement and parent");
var existing = new Il2CppSystem.Collections.Generic.List<ItemFeature>(); existing.Add(new() { parentItemUniqueId = 999, identifier = "other" });
store.savedItemFeatureList = existing;
graph = ForgeSaveGraphApi.Encode(bag);
Check(ReferenceEquals(store.savedItemFeatureList, existing) && existing.Count == 1, "Encoding cannot append NPC features to the player's pending save table");
ES3.FailSerialize = true;
Reject(() => ForgeSaveGraphApi.Encode(bag), "Native feature serialization failure is reported"); ES3.FailSerialize = false;
Check(ReferenceEquals(store.savedItemFeatureList, existing) && existing.Count == 1, "Failed encoding restores the exact previous feature table");
Reject(() => ForgeSaveGraphApi.Decode(graph, new HashSet<int> { 11 }), "Existing child UID rejects the whole graph");
var document = JsonNode.Parse(graph)!.AsObject(); document["forgeItemFeatures"] = "not-base64";
Reject(() => ForgeSaveGraphApi.Decode(document.ToJsonString()), "Malformed feature payload is rejected");
document.Remove("forgeItemFeatures");
Check(ForgeSaveGraphApi.Decode(document.ToJsonString()).Nested.Single().uniqueId == 11, "Existing native-only graphs remain readable");

// Reproduce native inventory saves: features live outside SaveItemNode JSON.
bag.AddItemFeature(new() { identifier = "damaged", actualValue = 37, isDisabled = true });
var inventory = new GameItem { uniqueId = 1 };
var removedMachine = new GameItem { uniqueId = 20, identifier = "missing" };
removedMachine.AddItemFeature(new() { identifier = "machine-condition", actualValue = 9 });
inventory.Nested.Add(removedMachine); removedMachine.Nested.Add(bag);
store.savedItemFeatureList = new();
var savedNodes = SaveManager.EncodeNodes(inventory);
store.savedItemFeatureList.Add(new() { parentItemUniqueId = 999, identifier = "unrelated" });
string nativeInventory = UnityEngine.JsonUtility.ToJson(new SaveState { saveItems = savedNodes });
var plan = MissingItemSaveMigration.Plan(new Dictionary<string, string?> { ["mainInvJSON"] = nativeInventory }, [], 100,
    (id, _) => id != "missing");
Check(plan.RemovedNodes == 1 && plan.Recoveries.Count == 1, "Removing a provider extracts the complete registered container subtree");
string receipt = ForgeSaveGraphApi.CaptureRecoveryFeatures(plan.Recoveries.Single(), store.savedItemFeatureList);
Check(store.savedItemFeatureList.Count == 4, "Capturing a recovery must leave the native table intact for other inventory items");
store.savedItemFeatureList.Clear(); // LoadGame clears its table before deferred delivery.
var returned = ForgeSaveGraphApi.Decode(receipt);
Check(returned.uniqueId == 10 && returned.itemFeatures.Single() is { identifier: "damaged", actualValue: 37, isDisabled: true, parentItemUniqueId: 10 },
    "Recovered container keeps its saved feature value and disabled flag after native load clears the table");
Check(returned.Nested.Single().itemFeatures.Single() is { identifier: "genuine", actualValue: 160, isDisabled: false, parentItemUniqueId: 11 },
    "Recovered contents keep their own features without inheriting removed or unrelated item features");
returned.Destroy(); // No space: discard the live attempt and persist the receipt.
string persisted = JsonSerializer.Serialize(new[] { receipt });
string reloaded = JsonSerializer.Deserialize<string[]>(persisted)!.Single();
var retryPlan = MissingItemSaveMigration.Plan(plan.Inventories.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
    [reloaded], plan.Cash, (id, _) => id != "missing");
Check(retryPlan.Refund == 0 && retryPlan.RemovedNodes == 0, "A pending delivery must not repeat provider removal or refund on reload");
string retry = ForgeSaveGraphApi.CaptureRecoveryFeatures(retryPlan.Recoveries.Single(), store.savedItemFeatureList);
Check(retry == receipt, "A pending receipt must retain its feature payload when the new native save table is empty");
Check(ForgeSaveGraphApi.Decode(retry).Nested.Single().itemFeatures.Single().actualValue == 160,
    "Delivery after a full inventory and another save/load must restore the original condition value");

// Another provider can disappear while the first recovery is still pending.
var split = MissingItemSaveMigration.Plan(new Dictionary<string, string?>(), [retry], plan.Cash, (id, _) => id != "fanny_pack");
Check(split.RemovedNodes == 1 && split.Recoveries.Count == 1, "Removing the pending container must retain its registered contents");
Reject(() => ForgeSaveGraphApi.Decode(split.Recoveries.Single()), "Features for removed parents must be rejected before native decode");
string splitReceipt = ForgeSaveGraphApi.CaptureRecoveryFeatures(split.Recoveries.Single(), store.savedItemFeatureList);
var splitItem = ForgeSaveGraphApi.Decode(splitReceipt);
Check(splitItem.uniqueId == 11 && splitItem.itemFeatures.Single() is { identifier: "genuine", actualValue: 160 },
    "Splitting a pending recovery must keep only the surviving node's feature records");

string emptyReceipt = ForgeSaveGraphApi.CaptureRecoveryFeatures(plan.Recoveries.Single(), store.savedItemFeatureList);
var emptyState = UnityEngine.JsonUtility.FromJson(emptyReceipt, Il2CppType.Of<SaveState>()).Cast<SaveState>();
var emptyItem = SaveManager.DecodeNodes(emptyState.saveItems);
foreach (var item in ForgeSaveGraphApi.Items(emptyItem)) item.AddItemFeature(new() { identifier = "factory-default" });
ForgeSaveGraphApi.RestoreFeatures(emptyState, ForgeSaveGraphApi.DecodeFeatures(emptyReceipt, emptyState));
Check(ForgeSaveGraphApi.Items(emptyItem).All(item => item.itemFeatures.Count == 0),
    "An explicitly empty saved feature table must replace factory defaults on every recovered node");
ES3.FailSerialize = true;
Reject(() => ForgeSaveGraphApi.CaptureRecoveryFeatures(plan.Recoveries.Single(), existing),
    "Recovery feature serialization failure must be reported before committing cleanup");
ES3.FailSerialize = false;
Check(existing.Count == 1 && existing.Single().parentItemUniqueId == 999,
    "A failed recovery capture must not consume or change the native feature table");
Reject(() => ForgeSaveGraphApi.CaptureRecoveryFeatures(plan.Recoveries.Single(), null),
    "Missing feature data must not be silently committed as an empty recovery table");
PlayerStore.instance = null;
Reject(() => ForgeSaveGraphApi.Encode(bag), "Encoding without an existing player context cannot create a singleton");
Console.WriteLine($"PASS {checks} native graph adapter checks.");
