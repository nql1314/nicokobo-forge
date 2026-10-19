using System.Text.Json;
using Il2Cpp;
using Nicokobo.Forge;

int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Reject(Action action, string message)
{ bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, message); }
PlayerStore World(bool loaded = true)
{
    ForgeRunForkApi.Uninstall(); ForgeRunForkApi.Install(true, _ => { });
    var store = PlayerStore.instance = new();
    if (loaded) ForgeLifecycleApi.Loaded(store);
    return store;
}
string State(string run, int slot, bool pending = false) => JsonSerializer.Serialize(new { run, slot, pending, claimed = new[] { "paid-reward" }, uid = 123 });
string Migrate(string json, ForgeRunIdentity source, ForgeRunIdentity destination)
{
    using var state = JsonDocument.Parse(json);
    if (state.RootElement.GetProperty("run").GetString() != source.RunId || state.RootElement.GetProperty("slot").GetInt32() != source.SlotId ||
        state.RootElement.GetProperty("pending").GetBoolean()) throw new InvalidOperationException("Unverified or unfinished source");
    return State(destination.RunId, destination.SlotId);
}
try
{
    var store = World();
    const string key = "test.owner.progress", key2 = "test.other.progress";
    store.modData[key] = State(store.runID, store.saveSlotId);
    store.modData[key2] = State(store.runID, store.saveSlotId);
    store.modData["foreign.untouched"] = "opaque-data";
    int rebounds = 0;
    using var first = ForgeRunForkApi.RegisterState("test.owner", key, Migrate, _ => rebounds++);
    using var second = ForgeRunForkApi.RegisterState("test.other", key2, Migrate);
    string original = store.modData[key];
    store.AtInitial = () => Check(ForgeRunForkApi.IsForking(store), "Fork scope was not published before content initializers");
    store.BeforeWrite = store.InitialSave; // Actual RestartAtAnyDay: fork inside outer WriteSlotFile.
    store.SaveGame();
    Check(store.Writes.Count == 2 && store.Allocations == 1 && rebounds == 1, "Nested fork saved/rebound an incorrect number of times");
    foreach (var write in store.Writes)
    {
        using var state = JsonDocument.Parse(write.Data[key]);
        Check(state.RootElement.GetProperty("run").GetString() == write.Run && state.RootElement.GetProperty("slot").GetInt32() == write.Slot,
            "Native file received a new identity with old modData");
        Check(write.Data[key] == write.Data[key2] && state.RootElement.GetProperty("uid").GetInt32() == 123 &&
            state.RootElement.GetProperty("claimed")[0].GetString() == "paid-reward" && write.Data["foreign.untouched"] == "opaque-data",
            "Migration lost receipts, changed UIDs, or touched another provider");
    }
    Check(!ForgeRunForkApi.IsForking(store) && original != store.modData[key], "Fork context leaked after completion");
    store.SaveGame(); Check(rebounds == 1, "A normal save repeated migration");
    store.InitialSave(); Check(rebounds == 2 && store.Allocations == 2, "A second branch did not use its current source identity");

    store = World(); store.modData[key] = State(store.runID, store.saveSlotId, pending: true);
    using var pending = ForgeRunForkApi.RegisterState("test.owner", key, Migrate);
    Reject(store.InitialSave, "An unresolved transaction forked");
    Check(store.Allocations == 0 && store.Writes.Count == 0 && !ForgeRunForkApi.IsForking(store), "Rejected preflight changed native allocation or leaked scope");

    store = World(); store.modData[key] = State(store.runID, store.saveSlotId);
    using var fail = ForgeRunForkApi.RegisterState("test.owner", key, (json, source, destination) =>
        source == destination ? Migrate(json, source, destination) : throw new InvalidOperationException("Migration fault"));
    original = store.modData[key];
    Reject(store.InitialSave, "Migration fault was swallowed");
    Check(store.Writes.Count == 0 && store.modData[key] == original, "Failed migration partially saved or replaced source JSON");
    Reject(store.SaveGame, "A failed allocation allowed an automatic retry");
    // A successful source reload retires the failed attempt, rather than guessing a repair.
    store.runID = "source-run"; store.saveSlotId = 2; ForgeLifecycleApi.Loaded(store);
    store.SaveGame(); Check(store.Writes.Count == 1, "Verified source reload did not restore saving");

    store = World(); store.modData[key] = State(store.runID, store.saveSlotId);
    using var good = ForgeRunForkApi.RegisterState("test.owner", key, Migrate);
    store.FailWrite = true;
    bool io = false; try { store.InitialSave(); } catch (IOException) { io = true; }
    Check(io && !ForgeRunForkApi.IsForking(store) && store.Writes.Count == 0, "Native write failure was hidden or leaked a fork");
    Reject(store.SaveGame, "Native write failure retried an uncertain fork");

    store = World(); store.SkipSave = true;
    Reject(store.InitialSave, "A skipped SaveGame was accepted as a successful fork");
    Check(store.Writes.Count == 0, "Skipped SaveGame unexpectedly wrote a file");
    foreach (bool corrupt in new[] { false, true })
    {
        store = World(); store.CorruptWrite = corrupt; store.SuppressWrite = !corrupt;
        bool rejectedReadback = false;
        try { store.InitialSave(); } catch (InvalidDataException) { rejectedReadback = true; }
        Check(rejectedReadback, "A missing or wrong-identity native file was accepted");
        Reject(store.SaveGame, "Failed readback allowed automatic fork retries");
    }

    store = World(loaded: false);
    Reject(store.InitialSave, "Unconfirmed loaded source was adopted");
    store.StartNewGame(); store.AtInitial = () => Check(!ForgeRunForkApi.IsForking(store), "A real new game was classified as a history fork");
    store.InitialSave(); Check(store.Allocations == 1 && store.Writes.Count == 1, "A genuine initial save was blocked");

    store = World(); store.modData[key] = State(store.runID, store.saveSlotId);
    using var busy = ForgeRunForkApi.RegisterState("test.owner", key, Migrate, ready: _ => false);
    Reject(store.InitialSave, "A busy participant allowed native allocation");
    Check(store.Allocations == 0, "Busy preflight happened after native allocation");

    store = World(); store.saveSlotId++;
    Reject(store.SaveGame, "An unexplained native slot change wrote mismatched progress");
    Check(store.Writes.Count == 0, "Unknown identity change reached disk");
    store.saveSlotId--; store.SaveGame();
    Check(store.Writes.Count == 1, "Restoring the verified native address did not restore normal saving");

    store = World(); store.modData[key] = State(store.runID, store.saveSlotId);
    using var repair = ForgeRunForkApi.RegisterState("test.owner", key, Migrate);
    store.SaveGame();
    string snapshot = Path.Combine(UnityEngine.Application.persistentDataPath, SaveFiles.Slot(store.saveSlotId));
    original = store.modData[key]; store.runID = "damaged-branch"; store.saveSlotId = 23;
    ForgeLifecycleApi.Loaded(store);
    var repaired = ForgeRunForkApi.RepairFromSnapshot(store, snapshot);
    Check(repaired.ReboundInMemory && store.Writes.Count == 1 && store.modData[key] != original,
        "Verified recovery wrote a file or did not rebind progress");
    store.modData[key] = original + " ";
    var untouched = store.modData[key];
    Check(!ForgeRunForkApi.RepairFromSnapshot(store, snapshot).ReboundInMemory && store.modData[key] == untouched,
        "Unproven legacy recovery replaced a mismatched payload");
    Console.WriteLine($"Run fork checks passed: {checks} assertions (production hooks, offline native ports).");
}
finally
{
    ForgeRunForkApi.Uninstall();
    var directory = new DirectoryInfo(UnityEngine.Application.persistentDataPath);
    if (directory.Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetTempPath()) ||
        !directory.Name.StartsWith("forge-run-fork-check-", StringComparison.Ordinal)) throw new Exception("Unexpected test directory");
    foreach (var file in directory.GetFiles()) file.Delete();
    directory.Delete();
}
