using System.Text.Json;
using System.Text.RegularExpressions;
using Il2Cpp;

namespace Nicokobo.Forge;

public enum RunDataStatus
{
    Disabled, Invalid, Unavailable, Missing, Present, StagedInMemory,
    Conflict, Indeterminate
}

public sealed record RunDataResult(RunDataStatus Status, string? Json = null,
    string? Reason = null);

/// <summary>Owner-scoped access to the current run's native modData. Stage only
/// changes the in-memory dictionary. It does not claim a completed save.</summary>
public static class ForgeRunDataApi
{
    private static readonly Regex NamespacedId = new(
        "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant);
    private const int MaxJsonChars = ForgeNumbers.RunData.MaxJsonChars;
    private static bool _enabled;

    internal static void SetEnabled(bool enabled) => _enabled = enabled;

    public static RunDataResult Read(PlayerStore? store, string ownerId, string key)
    {
        if (!_enabled) return new(RunDataStatus.Disabled);
        if (!ValidKey(ownerId, key)) return new(RunDataStatus.Invalid, Reason: "Invalid owner or key");
        if (store == null) return new(RunDataStatus.Unavailable);
        try
        {
            var data = store.modData;
            if (data == null || string.IsNullOrWhiteSpace(store.runID) || store.saveSlotId < 0)
                return new(RunDataStatus.Unavailable);
            if (!data.ContainsKey(key)) return new(RunDataStatus.Missing);
            string json = data[key];
            return ValidJson(json)
                ? new(RunDataStatus.Present, json)
                : new(RunDataStatus.Invalid, Reason: "Stored JSON is malformed or too large");
        }
        catch (Exception ex)
        {
            return new(RunDataStatus.Unavailable, Reason: ex.GetType().Name);
        }
    }

    /// <param name="expectedJson">Null means the key must not exist. A present
    /// value must match byte-for-byte, even when JSON is semantically equivalent.</param>
    public static RunDataResult Stage(PlayerStore? store, string ownerId, string key,
        string runId, int slotId, string? expectedJson, string nextJson)
    {
        if (!_enabled) return new(RunDataStatus.Disabled);
        if (!ValidKey(ownerId, key) || string.IsNullOrWhiteSpace(runId) || slotId < 0 ||
            !ValidJson(nextJson) || expectedJson != null && !ValidJson(expectedJson))
            return new(RunDataStatus.Invalid, Reason: "Invalid identity, key or JSON");
        if (store == null) return new(RunDataStatus.Unavailable);
        bool attempted = false;
        try
        {
            var data = store.modData;
            if (data == null) return new(RunDataStatus.Unavailable);
            if (store.runID != runId || store.saveSlotId != slotId)
                return new(RunDataStatus.Conflict, Reason: "Run identity changed");
            bool present = data.ContainsKey(key);
            if (present != (expectedJson != null) ||
                present && data[key] != expectedJson)
                return new(RunDataStatus.Conflict, Reason: "Stored revision changed");
            if (store.runID != runId || store.saveSlotId != slotId ||
                data.ContainsKey(key) != present ||
                present && data[key] != expectedJson)
                return new(RunDataStatus.Conflict, Reason: "Preflight changed");
            attempted = true;
            if (present) data[key] = nextJson;
            else data.Add(key, nextJson);
            return data.ContainsKey(key) && data[key] == nextJson &&
                store.runID == runId && store.saveSlotId == slotId
                ? new(RunDataStatus.StagedInMemory, nextJson)
                : new(RunDataStatus.Indeterminate);
        }
        catch (Exception ex)
        {
            return new(attempted ? RunDataStatus.Indeterminate : RunDataStatus.Unavailable,
                Reason: ex.GetType().Name);
        }
    }

    private static bool ValidKey(string ownerId, string key) =>
        !string.IsNullOrWhiteSpace(ownerId) && NamespacedId.IsMatch(ownerId) &&
        !string.IsNullOrWhiteSpace(key) && NamespacedId.IsMatch(key) &&
        key.StartsWith(ownerId + ".", StringComparison.Ordinal);

    private static bool ValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxJsonChars) return false;
        try
        {
            using var document = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = ForgeNumbers.RunData.MaxJsonDepth });
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; }
    }
}
