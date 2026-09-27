using System.Text;
using System.Text.Json;

namespace Nicokobo.Forge;

internal enum NetworkUpgradeSaveStatus
{
    Matched, Mismatch, Missing, ChangedDuringRead, TooLarge, Malformed,
    Unavailable
}

internal sealed record NetworkUpgradeSaveResult(NetworkUpgradeSaveStatus Status,
    string Reason);

// The current game's ES3 writer permits numeric object keys, raw controls and
// non-JSON escapes. Read only the current slot's identity and purchase values.
internal static class NetworkUpgradeSaveReadback
{
    private const int MaxBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static NetworkUpgradeSaveResult Compare(string? directory,
        string runId, int slotId, string upgradeId, int state, int cash, int favor)
    {
        if (string.IsNullOrWhiteSpace(directory) ||
            !Guid.TryParse(runId, out var expectedRunId) ||
            expectedRunId == Guid.Empty || slotId < 0 ||
            string.IsNullOrWhiteSpace(upgradeId))
            return new(NetworkUpgradeSaveStatus.Unavailable, "Invalid save identity");
        var path = Path.Combine(directory, $"save_{slotId}.es3");
        try
        {
            if (!File.Exists(path))
                return new(NetworkUpgradeSaveStatus.Missing, "Slot file is missing");
            var before = new FileInfo(path);
            if (before.Length > MaxBytes)
                return new(NetworkUpgradeSaveStatus.TooLarge, "Slot file exceeds limit");
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > MaxBytes)
                    return new(NetworkUpgradeSaveStatus.TooLarge, "Slot file exceeds limit");
                bytes = new byte[checked((int)stream.Length)];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0)
                        return new(NetworkUpgradeSaveStatus.ChangedDuringRead,
                            "Slot file changed during read");
                    read += count;
                }
            }
            var after = new FileInfo(path);
            if (before.Length != bytes.Length || after.Length != bytes.Length ||
                before.LastWriteTimeUtc != after.LastWriteTimeUtc)
                return new(NetworkUpgradeSaveStatus.ChangedDuringRead,
                    "Slot file changed during read");
            using var document = JsonDocument.Parse(
                Normalize(StrictUtf8.GetString(bytes).TrimStart('\uFEFF')),
                new JsonDocumentOptions { MaxDepth = 256 });
            if (!Unique(document.RootElement, "playerStore", out var wrapper) ||
                !Unique(wrapper, "__type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                type.GetString() != "PlayerStore,Assembly-CSharp" ||
                !Unique(wrapper, "value", out var store) ||
                !Unique(store, "runID", out var savedRunId) ||
                savedRunId.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(savedRunId.GetString(), out var savedRun) ||
                !Unique(store, "saveSlotId", out var savedSlotId) ||
                savedSlotId.ValueKind != JsonValueKind.Number ||
                !savedSlotId.TryGetInt32(out int savedSlot) ||
                !Unique(store, "playerCash", out var savedCash) ||
                savedCash.ValueKind != JsonValueKind.Number ||
                !savedCash.TryGetInt32(out int savedCashValue) ||
                !Unique(store, "wildFavor", out var savedFavor) ||
                savedFavor.ValueKind != JsonValueKind.Number ||
                !savedFavor.TryGetInt32(out int savedFavorValue) ||
                !Unique(store, "networkUpgrade", out var upgrades) ||
                !Unique(upgrades, upgradeId, out var upgrade) ||
                !Unique(upgrade, "id", out var savedUpgradeId) ||
                savedUpgradeId.ValueKind != JsonValueKind.String ||
                !Unique(upgrade, "state", out var savedState) ||
                savedState.ValueKind != JsonValueKind.Number ||
                !savedState.TryGetInt32(out int savedStateValue))
                return new(NetworkUpgradeSaveStatus.Malformed,
                    "Required native save fields are missing or malformed");
            return savedRun == expectedRunId && savedSlot == slotId &&
                   savedUpgradeId.GetString() == upgradeId &&
                   savedStateValue == state && savedCashValue == cash &&
                   savedFavorValue == favor
                ? new(NetworkUpgradeSaveStatus.Matched, "Native slot values matched")
                : new(NetworkUpgradeSaveStatus.Mismatch,
                    "Native slot identity or purchase values differ");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or
            InvalidDataException or InvalidOperationException or OverflowException)
        {
            return new(NetworkUpgradeSaveStatus.Malformed, ex.GetType().Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return new(NetworkUpgradeSaveStatus.Unavailable, ex.GetType().Name);
        }
    }

    private static bool Unique(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        if (parent.ValueKind != JsonValueKind.Object) return false;
        bool found = false;
        foreach (var property in parent.EnumerateObject())
        {
            if (property.Name != name) continue;
            if (found) return false;
            found = true;
            value = property.Value;
        }
        return found;
    }

    private static string Normalize(string source)
    {
        var result = new StringBuilder(source.Length + 64);
        bool inString = false;
        for (int i = 0; i < source.Length;)
        {
            char c = source[i];
            if (inString)
            {
                if (c == '\\' && i + 1 < source.Length)
                {
                    char next = source[i + 1];
                    bool valid = "\"\\/bfnrt".Contains(next) ||
                        next == 'u' && i + 5 < source.Length &&
                        IsHex(source[i + 2]) && IsHex(source[i + 3]) &&
                        IsHex(source[i + 4]) && IsHex(source[i + 5]);
                    if (valid)
                    {
                        result.Append(c).Append(next);
                        i += 2;
                        continue;
                    }
                    result.Append("\\\\");
                    i++;
                    continue;
                }
                if (c < 0x20) result.Append("\\u").Append(((int)c).ToString("x4"));
                else result.Append(c);
                if (c == '"') inString = false;
                i++;
                continue;
            }
            if (c == '"')
            {
                inString = true;
                result.Append(c);
                i++;
                continue;
            }
            result.Append(c);
            i++;
            if (c is not ('{' or ',')) continue;
            int start = i;
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            int digitStart = i;
            if (i < source.Length && source[i] == '-') i++;
            int digits = i;
            while (i < source.Length && char.IsDigit(source[i])) i++;
            if (i == digits)
            {
                result.Append(source, start, i - start);
                continue;
            }
            int digitEnd = i;
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;
            if (i < source.Length && source[i] == ':')
            {
                result.Append(source, start, digitStart - start);
                result.Append('"').Append(source, digitStart, digitEnd - digitStart)
                    .Append('"');
                result.Append(source, digitEnd, i - digitEnd).Append(':');
                i++;
            }
            else result.Append(source, start, i - start);
        }
        return result.ToString();
    }

    private static bool IsHex(char value) => value is >= '0' and <= '9' or
        >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
