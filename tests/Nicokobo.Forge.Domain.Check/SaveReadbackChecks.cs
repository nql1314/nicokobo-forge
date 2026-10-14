using System.Text;
using System.Text.Json;
using Nicokobo.Forge;

internal static class SaveReadbackChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception(message); }
        void Throws(Action action, string message)
        { bool threw = false; try { action(); } catch { threw = true; } Check(threw, message); }
        const string connected = "{\"saveItems\":[{\"childItems\":[1]},{\"childItems\":[2]},{\"identifier\":\"item\",\"uniqueId\":42,\"unitCount\":3},{\"identifier\":\"orphan\",\"uniqueId\":43,\"unitCount\":1}]}";
        using (var graph = JsonDocument.Parse(connected))
        {
            var items = ForgeSaveReadbackApi.ReadInventoryItems(graph.RootElement);
            Check(items.Count == 1 && items[0].GetProperty("uniqueId").GetInt32() == 42 && items[0].GetProperty("unitCount").GetInt32() == 3,
                "Only reachable items through storage nodes can prove delivery; stacks remain intact");
            Throws(() => ForgeSaveReadbackApi.ReadInventoryItems(graph.RootElement, 2), "Graph node limit is enforced");
        }
        foreach (var badGraph in new[] { connected.Replace("[2]", "[0]"), connected.Replace("[2]", "[99]"),
                     connected.Replace("[2]", "[2,2]"), connected.Replace("[2]", "[2,3]").Replace("43", "42"),
                     connected.Replace("\"uniqueId\":42", "\"uniqueId\":42,\"uniqueId\":44") })
        {
            using var graph = JsonDocument.Parse(badGraph);
            Throws(() => ForgeSaveReadbackApi.ReadInventoryItems(graph.RootElement), "Corrupt edges or duplicate item identities cannot prove delivery");
        }
        using (var read = ForgeSaveReadbackApi.ReadBytes(Encoding.UTF8.GetBytes("\uFEFF{12:\"literal\\q\nline\",-3:7}")))
        {
            Check(read.Status == ForgeSaveReadStatus.Readable, "Native numeric keys, BOM and legacy strings were not read");
            Check(ForgeSaveReadbackApi.RequireProperty(read.Root, "12").GetString() == "literal\\q\nline" &&
                ForgeSaveReadbackApi.RequireProperty(read.Root, "-3").GetInt32() == 7, "Normalization changed stored values");
        }
        using (var document = ForgeSaveReadbackApi.Parse("{\"one\":1,\"one\":2,\"escaped\":\"\\u4e2d\\t\\\\\"}"))
        {
            Check(!ForgeSaveReadbackApi.TryGetUniqueProperty(document.RootElement, "one", out _, out _), "Duplicate key selected a value");
            Check(ForgeSaveReadbackApi.TryGetUniqueProperty(document.RootElement, "missing", out _, out bool present) && !present,
                "Missing optional key was malformed");
            Throws(() => ForgeSaveReadbackApi.RequireProperty(document.RootElement, "one"), "Required duplicate key accepted");
            Check(ForgeSaveReadbackApi.RequireProperty(document.RootElement, "escaped").GetString() == "中\t\\",
                "Valid escapes changed");
        }
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 0xff }, Encoding.UTF8.GetBytes("[]"),
                     Encoding.UTF8.GetBytes("{broken"), Encoding.UTF8.GetBytes("{\"a\":{\"b\":{}}}") })
        {
            using var read = ForgeSaveReadbackApi.ReadBytes(bytes, maxDepth: 2);
            Check(read.Status == ForgeSaveReadStatus.Malformed, "Malformed UTF8/JSON/depth was accepted");
        }
        using (var read = ForgeSaveReadbackApi.ReadBytes(Encoding.UTF8.GetBytes("{}"), maxBytes: 1))
            Check(read.Status == ForgeSaveReadStatus.TooLarge, "Oversize bytes parsed");
        using (var read = ForgeSaveReadbackApi.ReadFile("", maxBytes: 1))
            Check(read.Status == ForgeSaveReadStatus.Invalid, "Invalid path accepted");
        using (var read = ForgeSaveReadbackApi.ReadBytes(Encoding.UTF8.GetBytes("{}"), maxDepth: 0))
            Check(read.Status == ForgeSaveReadStatus.Invalid, "Unbounded JSON depth accepted");

        string path = Path.Combine(Path.GetTempPath(), "forge-readback-" + Guid.NewGuid().ToString("N") + ".es3");
        try
        {
            using (var missing = ForgeSaveReadbackApi.ReadFile(path))
                Check(missing.Status == ForgeSaveReadStatus.Missing, "Missing file not distinguished");
            var bytes = Encoding.UTF8.GetBytes("{\"modData\":{\"test.one\":\"{\\\"revision\\\":2}\"}}");
            File.WriteAllBytes(path, bytes);
            using (var read = ForgeSaveReadbackApi.ReadFile(path))
            {
                Check(read.Status == ForgeSaveReadStatus.Readable && read.ByteCount == bytes.Length,
                    "Actual file did not produce a bounded detached read");
                Check(ForgeSaveReadbackApi.RequireProperty(ForgeSaveReadbackApi.RequireProperty(read.Root, "modData"),
                    "test.one").GetString() == "{\"revision\":2}", "Mod state contents were rewritten");
                read.Dispose(); read.Dispose();
                Throws(() => { _ = read.Root; }, "Disposed document remained usable");
            }
            using (var large = ForgeSaveReadbackApi.ReadFile(path, maxBytes: 2))
                Check(large.Status == ForgeSaveReadStatus.TooLarge, "Oversize file was read");
            using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var busy = ForgeSaveReadbackApi.ReadFile(path))
                Check(busy.Status == ForgeSaveReadStatus.Unavailable, "Busy file was reported as a stable read");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "Readback modified slot bytes");
        }
        finally { File.Delete(path); }
        Console.WriteLine($"Save readback: {checks} assertions passed (bounded reads, strict UTF8, ES3 syntax, duplicate keys, disposal).");
    }
}
