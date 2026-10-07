using System.Text;
using System.Text.Json;

namespace Nicokobo.Forge;

internal sealed record MissingItemSavePlan(IReadOnlyDictionary<string, string> Inventories,
    IReadOnlyList<string> Recoveries, int Cash, long Refund, int RemovedNodes);

// Works on native saved graphs without constructing items. Values and complete
// item state come from the save; no content Mod identities or prices live here.
internal static class MissingItemSaveMigration
{
    internal static MissingItemSavePlan Plan(IReadOnlyDictionary<string, string?> inventories,
        IReadOnlyList<string> recoveries, int cash, Func<string, string, bool> available)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var recovered = new List<string>();
        var recoveryRoots = new Dictionary<int, string>();
        var refunded = new Dictionary<int, (string Id, long Value, int Count)>();
        long refund = 0;
        int removedCount = 0;
        foreach (var input in inventories.Select(pair => (pair.Key, pair.Value, Inventory: true))
                     .Concat(recoveries.Select((json, i) => (Key: "recovery:" + i, Value: (string?)json, Inventory: false))))
        {
            if (string.IsNullOrWhiteSpace(input.Value)) continue;
            using var document = ForgeSaveReadbackApi.Parse(input.Value);
            var nodes = Array(document.RootElement, "saveItems").ToArray();
            if (nodes.Length == 0) continue;
            var missing = Enumerable.Range(0, nodes.Length)
                .Where(i => !available(Text(nodes[i], "identifier"), Text(nodes[i], "itemType"))).ToHashSet();
            if (missing.Count == 0)
            {
                if (!input.Inventory) AddRecovery(input.Value);
                continue;
            }
            if (input.Inventory && missing.Contains(0))
                throw new InvalidDataException("Inventory root factory is unavailable: " + input.Key);

            // childItems stores UUIDs, not array positions. Kept identities and
            // inventory node numbers are never renumbered after deletion.
            var byUuid = new Dictionary<long, int>();
            for (int i = 0; i < nodes.Length; i++)
                if (!byUuid.TryAdd(Integer(nodes[i], "uuid"), i))
                    throw new InvalidDataException("Duplicate saved item UUID: " + input.Key);
            var children = new long[nodes.Length][];
            var slots = new int[nodes.Length][];
            var parent = Enumerable.Repeat(-1, nodes.Length).ToArray();
            for (int i = 0; i < nodes.Length; i++)
            {
                children[i] = Array(nodes[i], "childItems").Select(value => value.GetInt64()).ToArray();
                slots[i] = Array(nodes[i], "childItemInventoryNode").Select(value => value.GetInt32()).ToArray();
                if (children[i].Length != slots[i].Length || children[i].Any(id => !byUuid.ContainsKey(id)))
                    throw new InvalidDataException("Invalid saved child links: " + input.Key);
                foreach (long uuid in children[i])
                {
                    int child = byUuid[uuid];
                    if (child == 0 || parent[child] != -1)
                        throw new InvalidDataException("Cyclic/shared saved item: " + input.Key);
                    parent[child] = i;
                }
            }
            var reachable = Traverse([0]);
            var affected = Traverse(missing);
            removedCount = checked(removedCount + missing.Count);
            foreach (int index in missing)
            {
                var node = nodes[index];
                // Sold/unplaced/unowned items never generate player money.
                if (input.Key == "soldInvJSON" || !reachable.Contains(index) || !IsOwned(node)) continue;
                int uniqueId = checked((int)Integer(node, "uniqueId"));
                long value = Integer(node, "unitValue");
                int count = checked((int)Integer(node, "unitCount"));
                if (uniqueId == 0 || value < 0 || count < 0)
                    throw new InvalidDataException("Invalid refund identity/value: " + input.Key);
                var identity = (Text(node, "identifier"), value, count);
                if (refunded.TryGetValue(uniqueId, out var previous))
                {
                    if (previous != identity) throw new InvalidDataException("Conflicting refund identity: " + input.Key);
                    continue;
                }
                refunded.Add(uniqueId, identity);
                refund = checked(refund + checked(value * count));
            }

            // Keep registered descendants as separate complete native graphs.
            // Their placement is attempted only after all inventories finish
            // loading; a full inventory leaves them in persisted recovery data.
            foreach (int root in reachable.Where(i => !missing.Contains(i) &&
                         (parent[i] >= 0 && missing.Contains(parent[i]) || !input.Inventory && i == 0)))
            {
                if (input.Key == "soldInvJSON") continue;
                var keep = Traverse([root], stopAtMissing: true);
                AddRecovery(Write(root, keep));
            }
            if (input.Inventory)
            {
                var keep = Enumerable.Range(0, nodes.Length).Where(i => !affected.Contains(i)).ToHashSet();
                replacements.Add(input.Key, Write(0, keep));
            }

            HashSet<int> Traverse(IEnumerable<int> roots, bool stopAtMissing = false)
            {
                var visited = new HashSet<int>();
                var pending = new Stack<int>(roots);
                while (pending.TryPop(out int index))
                {
                    if (stopAtMissing && missing.Contains(index)) continue;
                    if (!visited.Add(index)) continue;
                    foreach (long child in children[index]) pending.Push(byUuid[child]);
                }
                return visited;
            }
            string Write(int root, HashSet<int> keep)
            {
                using var output = new MemoryStream();
                using (var writer = new Utf8JsonWriter(output))
                {
                    writer.WriteStartObject();
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        writer.WritePropertyName(property.Name);
                        if (!property.NameEquals("saveItems")) { property.Value.WriteTo(writer); continue; }
                        writer.WriteStartArray();
                        foreach (int i in new[] { root }.Concat(Enumerable.Range(0, nodes.Length).Where(i => i != root && keep.Contains(i))))
                        {
                            writer.WriteStartObject();
                            foreach (var field in nodes[i].EnumerateObject())
                            {
                                writer.WritePropertyName(field.Name);
                                if (field.Name is not ("childItems" or "childItemInventoryNode"))
                                { field.Value.WriteTo(writer); continue; }
                                writer.WriteStartArray();
                                for (int child = 0; child < children[i].Length; child++)
                                {
                                    if (!keep.Contains(byUuid[children[i][child]])) continue;
                                    if (field.NameEquals("childItems")) writer.WriteNumberValue(children[i][child]);
                                    else writer.WriteNumberValue(slots[i][child]);
                                }
                                writer.WriteEndArray();
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        return new(replacements, recovered.AsReadOnly(), checked((int)checked(cash + refund)), refund, removedCount);

        void AddRecovery(string json)
        {
            using var graph = ForgeSaveReadbackApi.Parse(json);
            int identity = checked((int)Integer(Array(graph.RootElement, "saveItems").First(), "uniqueId"));
            if (recoveryRoots.TryGetValue(identity, out string? previous))
            {
                if (previous != json) throw new InvalidDataException("Conflicting recovery identity: " + identity);
                return;
            }
            recoveryRoots.Add(identity, json);
            recovered.Add(json);
        }
    }

    private static string Text(JsonElement node, string key) =>
        ForgeSaveReadbackApi.RequireProperty(node, key).GetString()
            ?? throw new InvalidDataException("Null saved property: " + key);
    private static long Integer(JsonElement node, string key) =>
        ForgeSaveReadbackApi.RequireProperty(node, key).GetInt64();
    private static IEnumerable<JsonElement> Array(JsonElement node, string key)
    {
        var value = ForgeSaveReadbackApi.RequireProperty(node, key);
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid saved array: " + key);
        return value.EnumerateArray();
    }
    private static bool IsOwned(JsonElement node) => Array(node, "_keys")
        .Any(key => key.ValueKind == JsonValueKind.String && key.GetString() == "IS_OWNED_TAG");
}
