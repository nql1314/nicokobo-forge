using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nicokobo.Forge.LogisticsExtension;

public enum LogisticsConditionKind { Item, Type, Tag }

public sealed record LogisticsCondition(LogisticsConditionKind Kind, string Key);

public sealed record LogisticsItemFacts(string ItemId, string TypeKey,
    IReadOnlyCollection<string> ActualTags);

public enum LogisticsRuleDecision { Allowed, Denied, Invalid }

public sealed record LogisticsRuleResult(LogisticsRuleDecision Decision, string Reason);

/// <summary>Immutable eligibility rules for a memory card. Item tags must come from
/// the actual instance, not a directory template.</summary>
public sealed class LogisticsRules
{
    public bool BlacklistEnabled { get; }
    public bool WhitelistEnabled { get; }
    public IReadOnlyList<LogisticsCondition> Blacklist { get; }
    public IReadOnlyList<LogisticsCondition> Whitelist { get; }

    public LogisticsRules(bool blacklistEnabled, bool whitelistEnabled,
        IEnumerable<LogisticsCondition> blacklist,
        IEnumerable<LogisticsCondition> whitelist)
    {
        ArgumentNullException.ThrowIfNull(blacklist);
        ArgumentNullException.ThrowIfNull(whitelist);
        BlacklistEnabled = blacklistEnabled;
        WhitelistEnabled = whitelistEnabled;
        Blacklist = Array.AsReadOnly(blacklist.ToArray());
        Whitelist = Array.AsReadOnly(whitelist.ToArray());
    }

    public static LogisticsRules NewCard() => new(false, true,
        Array.Empty<LogisticsCondition>(), Array.Empty<LogisticsCondition>());

    /// <param name="isKnown">Checks a saved item/type/tag key against the current
    /// game and active content. Any unresolved active condition stops the consumer.</param>
    public LogisticsRuleResult Evaluate(LogisticsItemFacts item,
        Func<LogisticsCondition, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(isKnown);
        if (string.IsNullOrWhiteSpace(item.ItemId) ||
            string.IsNullOrWhiteSpace(item.TypeKey) || item.ActualTags == null)
            return new(LogisticsRuleDecision.Invalid, "Item facts are incomplete");
        foreach (var condition in ActiveConditions())
        {
            bool known;
            try { known = condition != null && isKnown(condition); }
            catch { known = false; }
            if (condition == null || !Enum.IsDefined(condition.Kind) ||
                string.IsNullOrWhiteSpace(condition.Key) || !known)
                return new(LogisticsRuleDecision.Invalid,
                    $"Unresolved active rule: {condition?.Kind}/{condition?.Key}");
        }
        if (BlacklistEnabled && Blacklist.Any(c => Matches(c, item)))
            return new(LogisticsRuleDecision.Denied, "Blacklisted");
        if (WhitelistEnabled && !Whitelist.Any(c => Matches(c, item)))
            return new(LogisticsRuleDecision.Denied, "Not whitelisted");
        return new(LogisticsRuleDecision.Allowed, "Allowed");
    }

    private IEnumerable<LogisticsCondition> ActiveConditions()
    {
        if (BlacklistEnabled)
            foreach (var condition in Blacklist) yield return condition;
        if (WhitelistEnabled)
            foreach (var condition in Whitelist) yield return condition;
    }

    private static bool Matches(LogisticsCondition condition, LogisticsItemFacts item) =>
        condition.Kind switch
        {
            LogisticsConditionKind.Item => string.Equals(condition.Key, item.ItemId,
                StringComparison.Ordinal),
            LogisticsConditionKind.Type => string.Equals(condition.Key, item.TypeKey,
                StringComparison.Ordinal),
            LogisticsConditionKind.Tag => item.ActualTags.Contains(condition.Key,
                StringComparer.Ordinal),
            _ => false
        };

    /// <summary>Portable rule export excludes instance, binding, network and save IDs.</summary>
    public string Export(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            throw new ArgumentException("Card name is required", nameof(cardName));
        return JsonSerializer.Serialize(new ExportData(1, cardName, BlacklistEnabled,
            WhitelistEnabled, Blacklist, Whitelist), ExportOptions);
    }

    private sealed record ExportData(int Version, string Name, bool BlacklistEnabled,
        bool WhitelistEnabled, IReadOnlyList<LogisticsCondition> Blacklist,
        IReadOnlyList<LogisticsCondition> Whitelist);

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>A shared card identity and revision. Edits require the revision read by
/// the editor; queued transfers must re-evaluate against the latest revision.</summary>
public sealed record LogisticsCard(string CardId, string Name, long Revision,
    LogisticsRules Rules)
{
    public LogisticsCard Edit(long expectedRevision, string name, LogisticsRules rules)
    {
        if (Revision != expectedRevision)
            throw new InvalidOperationException("Memory card changed during editing");
        if (string.IsNullOrWhiteSpace(name) || rules == null || Revision == long.MaxValue)
            throw new ArgumentException("Invalid memory card edit");
        return this with { Name = name, Rules = rules, Revision = Revision + 1 };
    }
}
