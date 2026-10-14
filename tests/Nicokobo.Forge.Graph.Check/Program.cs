using System.Text.Json.Nodes;
using Il2Cpp;
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
PlayerStore.instance = null;
Reject(() => ForgeSaveGraphApi.Encode(bag), "Encoding without an existing player context cannot create a singleton");
Console.WriteLine($"PASS {checks} native graph adapter checks.");
