using System.Text.Json;
using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars;

internal static class ScalarAliasChecks
{
    private sealed class LiveItem
    {
        public bool isDebugMenu => _isDebugMenu_k__BackingField;
        public bool _isDebugMenu_k__BackingField { get; set; }
        public long unitValue { get; set; }
        public int unitCount { get; set; }
    }
    private sealed class Node
    {
        public long UniqueId { get; set; }
        public Dictionary<string, JsonElement> Scalars { get; set; } = [];
        public Dictionary<string, Dictionary<string, JsonElement>> State { get; set; } = [];
        public Dictionary<string, Dictionary<string, JsonElement>> ModifiedState { get; set; } = [];
        public List<Node> Features { get; set; } = [];
        public List<Snapshot> SlotItems { get; set; } = [];
        public List<Snapshot>? Composition { get; set; }
    }
    private sealed class Snapshot
    {
        public string Identifier { get; set; } = "pcrepair.computer_case_t1";
        public string Payload { get; set; } = "{\"native-count\":3}";
        public int Count { get; set; } = 3;
        public List<Node> Nodes { get; set; } = [];
    }
    private sealed class FieldNode
    {
        public Dictionary<string, JsonElement> Scalars = [];
    }
    private static JsonElement J<T>(T value) => JsonSerializer.SerializeToElement(value);
    internal static void RunPCExpansion(string path, string gameDir, Action<bool, string> check)
    {
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            foreach (string directory in new[] { "MelonLoader/net6", "MelonLoader/Il2CppAssemblies" })
            {
                string candidate = Path.Combine(gameDir, directory, name.Name + ".dll");
                if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
            }
            return null;
        }
        AssemblyLoadContext.Default.Resolving += Resolve;
        try
        {
            // Execute only the original managed DTO validators, never Mod startup
            // or native game methods. No game process or player save is involved.
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var codec = assembly.GetType("PCExpansion.WorkroomItemCodec", true)!;
            var snapshot = codec.GetNestedType("Snapshot", BindingFlags.NonPublic)!;
            var node = codec.GetNestedType("Node", BindingFlags.NonPublic)!;
            var equivalent = codec.GetMethod("Equivalent", flags)!;
            var preserved = assembly.GetType("PCExpansion.WorkroomMachineRepair", true)!.GetMethod("VerifyPreserved", flags)!;
            object Snapshot(string scalar)
            {
                object value = Activator.CreateInstance(snapshot, true)!;
                snapshot.GetProperty("Identifier")!.SetValue(value, "pcrepair.computer_case_t1");
                snapshot.GetProperty("Count")!.SetValue(value, 3);
                snapshot.GetProperty("Payload")!.SetValue(value, "{}");
                object item = Activator.CreateInstance(node, true)!;
                node.GetProperty("Scalars")!.SetValue(item, new Dictionary<string, JsonElement> { [scalar] = J(false), ["unitCount"] = J(3) });
                ((IList)snapshot.GetProperty("Nodes")!.GetValue(value)!).Add(item);
                return value;
            }
            object NodeOf(object value) => ((IList)snapshot.GetProperty("Nodes")!.GetValue(value)!)[0]!;
            var old = Snapshot("isDebugMenu"); var current = Snapshot("_isDebugMenu_k__BackingField");
            check(!(bool)equivalent.Invoke(null, [old, current])!, "original PC Expansion comparison no longer reproduces alias mismatch");
            bool repairRejected = false;
            try { preserved.Invoke(null, [NodeOf(old), NodeOf(current)]); }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { repairRejected = true; }
            check(repairRejected, "original PC Expansion repair no longer reproduces alias mismatch");
            var a = ScalarAliases.ComparisonCopy(old); var b = ScalarAliases.ComparisonCopy(current);
            check((bool)equivalent.Invoke(null, [a, b])!, "original PC Expansion complete validator rejects normalized aliases");
            preserved.Invoke(null, [NodeOf(a), NodeOf(b)]);
            check(true, "original PC Expansion repair validator accepts normalized aliases");
            snapshot.GetProperty("Count")!.SetValue(b, 99);
            check(!(bool)equivalent.Invoke(null, [a, b])!, "original PC Expansion quantity check was bypassed");
            b = ScalarAliases.ComparisonCopy(current); snapshot.GetProperty("Payload")!.SetValue(b, "{\"changed\":true}");
            check(!(bool)equivalent.Invoke(null, [a, b])!, "original PC Expansion payload check was bypassed");
            check(((Dictionary<string, JsonElement>)node.GetProperty("Scalars")!.GetValue(NodeOf(old))!).ContainsKey("isDebugMenu"),
                "original PC Expansion source record was mutated");

            // Exercise the shipped prefix through the game's actual HarmonyX.
            // Calling ComparisonCopy directly cannot verify argument propagation.
            var compatibility = AssemblyLoadContext.Default.LoadFromAssemblyPath(
                Path.Combine(AppContext.BaseDirectory, "Nicokobo.CompatibilityPatches.dll"));
            var prefix = compatibility.GetType(
                "Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars.PCExpansionScalarsPatch", true)!
                .GetMethod("BeforeCompare", flags)!;
            var harmony = new HarmonyLib.Harmony("nicokobo.compatibility.scalar-alias-checks");
            try
            {
                harmony.Patch(equivalent, prefix: new HarmonyLib.HarmonyMethod(prefix));
                harmony.Patch(preserved, prefix: new HarmonyLib.HarmonyMethod(prefix));
                check((bool)equivalent.Invoke(null, [old, current])!,
                    "installed comparison prefix did not pass normalized arguments to the original validator");
                preserved.Invoke(null, [NodeOf(old), NodeOf(current)]);
                check(true, "installed repair prefix passes normalized arguments");
                var different = Snapshot("_isDebugMenu_k__BackingField");
                snapshot.GetProperty("Count")!.SetValue(different, 99);
                check(!(bool)equivalent.Invoke(null, [old, different])!,
                    "installed comparison prefix bypassed the original quantity validator");
                different = Snapshot("_isDebugMenu_k__BackingField");
                snapshot.GetProperty("Payload")!.SetValue(different, "{\"changed\":true}");
                check(!(bool)equivalent.Invoke(null, [old, different])!,
                    "installed comparison prefix bypassed the original payload validator");
                check(((Dictionary<string, JsonElement>)node.GetProperty("Scalars")!.GetValue(NodeOf(old))!).ContainsKey("isDebugMenu"),
                    "installed comparison prefix changed the source record");
            }
            finally { harmony.UnpatchSelf(); }
        }
        finally { AssemblyLoadContext.Default.Resolving -= Resolve; }
    }

    internal static void Run(Action<bool, string> check)
    {
        var live = new LiveItem();
        bool originalRejected = false;
        try { typeof(LiveItem).GetProperty("isDebugMenu")!.SetValue(live, true); }
        catch (ArgumentException) { originalRejected = true; }
        check(originalRejected, "baseline no longer reproduces the readonly property failure");
        var source = new Dictionary<string, JsonElement>
        {
            ["isDebugMenu"] = J(true), ["unitCount"] = J(3), ["unitValue"] = J(9007199254740993L)
        };
        var restored = ScalarAliases.RestoreFields(typeof(LiveItem), source);
        foreach (var pair in restored)
        {
            var property = typeof(LiveItem).GetProperty(pair.Key)!;
            property.SetValue(live, pair.Value.Deserialize(property.PropertyType));
        }
        check(live._isDebugMenu_k__BackingField && live.unitCount == 3 && live.unitValue == 9007199254740993L,
            "restoration lost a value or 64-bit precision");
        check(source.ContainsKey("isDebugMenu") && !source.ContainsKey("_isDebugMenu_k__BackingField"), "source fields were rewritten");
        check(ScalarAliases.Name("_isDebugMenu_k__BackingField") == "isDebugMenu" &&
            ScalarAliases.Name("<isDebugMenu>k__BackingField") == "isDebugMenu", "backing aliases did not canonicalize");
        check(ScalarAliases.Name("PSTide._isDebugMenu_k__BackingField") == "PSTide._isDebugMenu_k__BackingField", "ordinary field name was shortened");

        void Rejected(Dictionary<string, JsonElement> fields, string name)
        {
            bool rejected = false;
            try { ScalarAliases.RestoreFields(typeof(LiveItem), fields); }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, name);
        }
        Rejected(new() { ["removedGameplayField"] = J(7) }, "unsupported field was silently dropped");
        Rejected(new() { ["isDebugMenu"] = J(true), ["_isDebugMenu_k__BackingField"] = J(false) }, "conflicting aliases were accepted");
        check(ScalarAliases.RestoreFields(typeof(LiveItem), new() { ["isDebugMenu"] = J(true), ["_isDebugMenu_k__BackingField"] = J(true) }).Count == 1,
            "equivalent aliases were not merged");

        var old = new Snapshot { Nodes = [new Node
        {
            UniqueId = 31, Scalars = source,
            State = new() { ["PSTide._tag_k__BackingField"] = new() { ["<score>k__BackingField"] = J(12) } },
            ModifiedState = new() { ["WINE_SCORE_INT"] = new() { ["_amount_k__BackingField"] = J(86) } },
            Features = [new Node { Scalars = new() { ["_category_k__BackingField"] = J("quality") } }],
            SlotItems = [new Snapshot { Count = 2, Nodes = [new Node { Scalars = new() { ["_unitCount_k__BackingField"] = J(2) } }] }],
            Composition = [new Snapshot { Count = 1, Nodes = [] }]
        }] };
        var current = new Snapshot { Nodes = [new Node
        {
            UniqueId = 31, Scalars = new() { ["_isDebugMenu_k__BackingField"] = J(true), ["unitCount"] = J(3), ["unitValue"] = J(9007199254740993L) },
            State = new() { ["PSTide._tag_k__BackingField"] = new() { ["score"] = J(12) } },
            ModifiedState = new() { ["WINE_SCORE_INT"] = new() { ["amount"] = J(86) } },
            Features = [new Node { Scalars = new() { ["category"] = J("quality") } }],
            SlotItems = [new Snapshot { Count = 2, Nodes = [new Node { Scalars = new() { ["unitCount"] = J(2) } }] }],
            Composition = [new Snapshot { Count = 1, Nodes = [] }]
        }] };
        string oldBytes = JsonSerializer.Serialize(old), currentBytes = JsonSerializer.Serialize(current);
        check(oldBytes != currentBytes, "baseline no longer reproduces the field-name mismatch");
        var a = (Snapshot)ScalarAliases.ComparisonCopy(old);
        var b = (Snapshot)ScalarAliases.ComparisonCopy(current);
        check(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "equivalent snapshot aliases still differ");
        check(JsonSerializer.Serialize(old) == oldBytes && JsonSerializer.Serialize(current) == currentBytes, "comparison changed an input snapshot");
        check(a.Payload == old.Payload && a.Count == old.Count && a.Nodes[0].UniqueId == 31, "comparison changed payload, units or identity");
        check(a.Nodes[0].State.ContainsKey("PSTide._tag_k__BackingField"), "a permanent tag identifier was renamed");
        check(a.Nodes[0].ModifiedState["WINE_SCORE_INT"]["amount"].GetInt32() == 86, "wine tag value changed");
        check(a.Nodes[0].Features[0].Scalars["category"].GetString() == "quality", "feature scalar was lost");
        check(a.Nodes[0].SlotItems[0].Nodes[0].Scalars["unitCount"].GetInt32() == 2 && a.Nodes[0].Composition![0].Count == 1, "nested inventory or composition was lost");
        check(!ReferenceEquals(a.Nodes, old.Nodes) && !ReferenceEquals(a.Nodes[0].Scalars, old.Nodes[0].Scalars), "comparison shares mutable scalar dictionaries");

        b.Nodes[0].Scalars["unitCount"] = J(99);
        check(JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b), "quantity mismatch was bypassed");
        b = (Snapshot)ScalarAliases.ComparisonCopy(current); b.Payload = "{\"native-count\":99}";
        check(JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b), "native payload mismatch was bypassed");
        b = (Snapshot)ScalarAliases.ComparisonCopy(current); b.Nodes[0].State["PSTide._tag_k__BackingField"]["score"] = J(99);
        check(JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b), "permanent tag mismatch was bypassed");
        b = (Snapshot)ScalarAliases.ComparisonCopy(current); b.Nodes[0].UniqueId++;
        check(JsonSerializer.Serialize(a) != JsonSerializer.Serialize(b), "identity mismatch was bypassed");
        var fields = new FieldNode { Scalars = new() { ["isDebugMenu"] = J(true) } };
        var copied = (FieldNode)ScalarAliases.ComparisonCopy(fields);
        check(!ReferenceEquals(fields.Scalars, copied.Scalars), "field-based snapshot DTO was mutated");
    }
}

