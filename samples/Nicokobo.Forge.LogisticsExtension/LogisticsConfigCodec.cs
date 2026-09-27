using System.Text.Json;

namespace Nicokobo.Forge.LogisticsExtension;

public sealed record LogisticsNodeConfig(string InstanceId, bool Push,
    LogisticsNodeBinding Binding);

/// <summary>Run-scoped configuration only. Network item payloads are deliberately
/// outside this codec until the native item serializer is verified.</summary>
public sealed record LogisticsConfig(string RunId, int SlotId, long Revision,
    bool NetworkUnlocked, IReadOnlyList<LogisticsCard> Cards,
    IReadOnlyList<LogisticsNodeConfig> Nodes,
    IReadOnlyList<LogisticsNetwork> Networks);

public enum LogisticsConfigReadStatus
{
    Ready, Missing, Malformed, FutureVersion, IdentityMismatch
}

public sealed record LogisticsConfigRead(LogisticsConfigReadStatus Status,
    LogisticsConfig? Config = null);

public static class LogisticsConfigCodec
{
    private const int SchemaVersion = 1;
    private const int MaxJsonChars = 1024 * 1024;
    private const int MaxEntries = 4096;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static string Write(LogisticsConfig config)
    {
        if (!Valid(config)) throw new ArgumentException("Invalid logistics configuration");
        var dto = new ConfigDto(SchemaVersion, config.RunId, config.SlotId,
            config.Revision, config.NetworkUnlocked,
            config.Cards.Select(c => new CardDto(c.CardId, c.Name, c.Revision,
                c.Rules.BlacklistEnabled, c.Rules.WhitelistEnabled,
                c.Rules.Blacklist.ToArray(), c.Rules.Whitelist.ToArray())).ToArray(),
            config.Nodes.ToArray(), config.Networks.ToArray());
        string json = JsonSerializer.Serialize(dto, Options);
        if (json.Length > MaxJsonChars)
            throw new ArgumentException("Logistics configuration exceeds size limit");
        return json;
    }

    public static LogisticsConfigRead Read(string? json, string runId, int slotId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new(LogisticsConfigReadStatus.Missing);
        if (json.Length > MaxJsonChars)
            return new(LogisticsConfigReadStatus.Malformed);
        try
        {
            using var document = JsonDocument.Parse(json,
                new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("version", out var version) ||
                !version.TryGetInt32(out int schema))
                return new(LogisticsConfigReadStatus.Malformed);
            if (schema > SchemaVersion)
                return new(LogisticsConfigReadStatus.FutureVersion);
            if (schema != SchemaVersion)
                return new(LogisticsConfigReadStatus.Malformed);
            var dto = JsonSerializer.Deserialize<ConfigDto>(json, Options);
            if (dto == null || dto.Cards == null || dto.Nodes == null ||
                dto.Networks == null || dto.Cards.Length > MaxEntries ||
                dto.Nodes.Length > MaxEntries || dto.Networks.Length > MaxEntries ||
                dto.Cards.Any(c => c == null || c.Blacklist == null ||
                    c.Whitelist == null || c.Blacklist.Length > MaxEntries ||
                    c.Whitelist.Length > MaxEntries))
                return new(LogisticsConfigReadStatus.Malformed);
            var config = new LogisticsConfig(dto.RunId, dto.SlotId, dto.Revision,
                dto.NetworkUnlocked,
                dto.Cards.Select(c => new LogisticsCard(c.CardId, c.Name,
                    c.Revision, new LogisticsRules(c.BlacklistEnabled,
                        c.WhitelistEnabled, c.Blacklist, c.Whitelist))).ToArray(),
                dto.Nodes, dto.Networks);
            if (!Valid(config)) return new(LogisticsConfigReadStatus.Malformed);
            return config.RunId == runId && config.SlotId == slotId
                ? new(LogisticsConfigReadStatus.Ready, config)
                : new(LogisticsConfigReadStatus.IdentityMismatch);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or
            InvalidOperationException or OverflowException)
        {
            return new(LogisticsConfigReadStatus.Malformed);
        }
    }

    private static bool Valid(LogisticsConfig config)
    {
        if (config == null || !Guid.TryParse(config.RunId, out var run) ||
            run == Guid.Empty || config.SlotId < 0 || config.Revision < 0 ||
            config.Cards == null || config.Nodes == null || config.Networks == null ||
            config.Cards.Count > MaxEntries || config.Nodes.Count > MaxEntries ||
            config.Networks.Count > MaxEntries)
            return false;
        if (config.Cards.Any(c => c == null || !Id(c.CardId) ||
            string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 128 ||
            c.Revision < 0 || c.Rules == null ||
            c.Rules.Blacklist.Count > MaxEntries ||
            c.Rules.Whitelist.Count > MaxEntries ||
            c.Rules.Blacklist.Concat(c.Rules.Whitelist).Any(rule =>
                rule == null || !Enum.IsDefined(rule.Kind) || !Id(rule.Key))))
            return false;
        if (config.Nodes.Any(n => n == null || !Id(n.InstanceId) ||
            n.Binding == null ||
            n.Binding.CardId != null && !Id(n.Binding.CardId) ||
            n.Binding.NetworkId != null && !Id(n.Binding.NetworkId)))
            return false;
        if (config.Networks.Any(n => n == null || !Id(n.Id) ||
            string.IsNullOrWhiteSpace(n.Name) || n.Name.Length > 128 ||
            n.CardId != null && !Id(n.CardId) || n.IsMain && n.CardId != null))
            return false;
        return config.Cards.Select(c => c.CardId).Distinct(StringComparer.Ordinal).Count() ==
                   config.Cards.Count &&
               config.Nodes.Select(n => n.InstanceId).Distinct(StringComparer.Ordinal).Count() ==
                   config.Nodes.Count &&
               config.Networks.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() ==
                   config.Networks.Count &&
               config.Networks.Count(n => n.IsMain) == 1;
    }

    private static bool Id(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128;

    private sealed record ConfigDto(int Version, string RunId, int SlotId,
        long Revision, bool NetworkUnlocked, CardDto[] Cards,
        LogisticsNodeConfig[] Nodes, LogisticsNetwork[] Networks);
    private sealed record CardDto(string CardId, string Name, long Revision,
        bool BlacklistEnabled, bool WhitelistEnabled,
        LogisticsCondition[] Blacklist, LogisticsCondition[] Whitelist);
}
