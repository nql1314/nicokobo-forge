using System.Text;
using System.Text.Json;

namespace Nicokobo.Forge.Workshop;

internal sealed record AchievementSaveResult(bool Matched, string Reason, RewardReceipt[] Items);

// Read the actual native slot bytes. A staged modData string is not a saved claim.
internal static class AchievementSaveReadback
{
    internal static AchievementSaveResult Compare(string path, string runId, int slotId,
        string expectedJson, PendingReward? reward = null)
    {
        using var read = ForgeSaveReadbackApi.ReadFile(path,
            ForgeNumbers.Achievements.MaxSaveBytes, ForgeNumbers.RunData.MaxJsonDepth);
        return read.Status == ForgeSaveReadStatus.Readable
            ? CompareRoot(read.Root, runId, slotId, expectedJson, reward)
            : new(false, read.Reason ?? read.Status.ToString(), []);
    }

    internal static AchievementSaveResult CompareBytes(byte[] bytes, string runId, int slotId,
        string expectedJson, PendingReward? reward = null)
    {
        using var read = ForgeSaveReadbackApi.ReadBytes(bytes,
            ForgeNumbers.Achievements.MaxSaveBytes, ForgeNumbers.RunData.MaxJsonDepth);
        return read.Status == ForgeSaveReadStatus.Readable
            ? CompareRoot(read.Root, runId, slotId, expectedJson, reward)
            : new(false, read.Reason ?? read.Status.ToString(), []);
    }

    private static AchievementSaveResult CompareRoot(JsonElement root, string runId, int slotId,
        string expectedJson, PendingReward? reward)
    {
        try
        {
            var wrapper = Unique(root, "playerStore");
            if (Unique(wrapper, "__type").GetString() != "PlayerStore,Assembly-CSharp")
                throw new InvalidDataException("Wrong playerStore type");
            var store = Unique(wrapper, "value");
            if (Unique(store, "runID").GetString() != runId || Unique(store, "saveSlotId").GetInt32() != slotId)
                return new(false, "Run or slot mismatch", []);
            var data = Unique(store, "modData");
            if (Unique(data, AchievementRules.SaveKey).GetString() != expectedJson)
                return new(false, "Achievement state not saved", []);
            if (reward == null || reward.Items.Length == 0) return new(true, "State matched", []);
            var owned = new List<RewardReceipt>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in store.EnumerateObject())
            {
                // soldInvJSON is not a player inventory. The grant itself always lands in mainInvJSON.
                if (property.Name == "soldInvJSON" || !(property.Name.EndsWith("InvJSON", StringComparison.Ordinal) ||
                    property.Name.EndsWith("InventoryJSON", StringComparison.Ordinal))) continue;
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate saved inventory");
                if (property.Value.ValueKind == JsonValueKind.Null) continue;
                string? encoded = property.Value.GetString();
                if (string.IsNullOrWhiteSpace(encoded)) continue;
                using var inventory = ForgeSaveReadbackApi.Parse(encoded, ForgeNumbers.RunData.MaxJsonDepth);
                var items = Unique(inventory.RootElement, "saveItems");
                if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() is <= 0 or > ForgeNumbers.Inventory.MaxRunItems)
                    throw new InvalidDataException("Invalid saved item graph");
                var visited = new HashSet<int>();
                var pending = new Stack<int>(); pending.Push(0);
                while (pending.TryPop(out int index))
                {
                    if (index < 0 || index >= items.GetArrayLength() || !visited.Add(index))
                        throw new InvalidDataException("Invalid saved graph edge");
                    var item = items[index];
                    // Inventory nodes also appear in saveItems. Only actual positive unit items are receipts.
                    if (index > 0 && Optional(item, "unitCount", out var unitValue) && unitValue.TryGetInt32(out int units) && units > 0 &&
                        Optional(item, "uniqueId", out var uidValue) && uidValue.TryGetInt32(out int uid) && uid > 0)
                        owned.Add(new(Unique(item, "identifier").GetString()!, uid, units));
                    if (!Optional(item, "childItems", out var children)) continue;
                    if (children.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid saved children");
                    foreach (var child in children.EnumerateArray())
                    {
                        int next = child.GetInt32();
                        if (next <= 0) throw new InvalidDataException("Invalid child index");
                        pending.Push(next);
                    }
                }
            }
            bool matched = AchievementRules.ReceiptsMatch(reward, owned);
            return new(matched, matched ? "State and reward identities matched" : "Reward identities not matched", owned.ToArray());
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or InvalidDataException or
            InvalidOperationException or FormatException or OverflowException)
        { return new(false, ex.Message, []); }
    }

    private static JsonElement Unique(JsonElement parent, string name) =>
        ForgeSaveReadbackApi.RequireProperty(parent, name);
    private static bool Optional(JsonElement parent, string name, out JsonElement value)
    {
        if (!ForgeSaveReadbackApi.TryGetUniqueProperty(parent, name, out value, out bool present))
            throw new InvalidDataException("Invalid or duplicate property: " + name);
        return present;
    }
}
