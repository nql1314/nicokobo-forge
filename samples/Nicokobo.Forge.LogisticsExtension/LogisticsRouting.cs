namespace Nicokobo.Forge.LogisticsExtension;

public enum LogisticsEndpointKind { NearbyContainers, Network }
public enum LogisticsRouteStatus { Ready, MissingCard, MissingNetwork, InvalidBinding }

public sealed record LogisticsNodeBinding(string? CardId, string? NetworkId);
public sealed record LogisticsNetwork(string Id, string Name, bool IsMain, string? CardId);
public sealed record LogisticsRoute(LogisticsRouteStatus Status,
    LogisticsEndpointKind Endpoint, string? NetworkId, string Reason);

/// <summary>Fail-closed routing and admission policy. Physical inventory movement
/// and durable storage require a build-specific adapter with readback.</summary>
public static class LogisticsRouting
{
    public static LogisticsRoute ResolvePush(LogisticsNodeBinding binding,
        bool networkUnlocked, string mainNetworkId,
        IReadOnlyDictionary<string, LogisticsCard> cards,
        IReadOnlyDictionary<string, LogisticsNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(cards);
        if (string.IsNullOrWhiteSpace(binding.CardId) ||
            !cards.ContainsKey(binding.CardId))
            return new(LogisticsRouteStatus.MissingCard,
                LogisticsEndpointKind.NearbyContainers, null,
                "Push node requires an accessible memory card");
        return ResolveEndpoint(binding.NetworkId, networkUnlocked, mainNetworkId, networks);
    }

    public static LogisticsRoute ResolvePull(LogisticsNodeBinding binding,
        bool networkUnlocked, string mainNetworkId,
        IReadOnlyDictionary<string, LogisticsNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return ResolveEndpoint(binding.NetworkId, networkUnlocked, mainNetworkId, networks);
    }

    private static LogisticsRoute ResolveEndpoint(string? requestedNetworkId,
        bool unlocked, string mainNetworkId,
        IReadOnlyDictionary<string, LogisticsNetwork> networks)
    {
        ArgumentNullException.ThrowIfNull(networks);
        if (!unlocked)
            return new(LogisticsRouteStatus.Ready,
                LogisticsEndpointKind.NearbyContainers, null, "Nearby containers");
        var id = requestedNetworkId ?? mainNetworkId;
        if (string.IsNullOrWhiteSpace(id) || !networks.TryGetValue(id, out var network))
            return new(LogisticsRouteStatus.MissingNetwork,
                LogisticsEndpointKind.Network, id, "Bound network is unavailable");
        if (requestedNetworkId == null && !network.IsMain)
            return new(LogisticsRouteStatus.InvalidBinding,
                LogisticsEndpointKind.Network, id, "Default network is not main");
        return new(LogisticsRouteStatus.Ready,
            LogisticsEndpointKind.Network, id, network.Name);
    }

    public static LogisticsRuleResult CanDeposit(LogisticsNetwork network,
        LogisticsItemFacts item, IReadOnlyDictionary<string, LogisticsCard> cards,
        Func<LogisticsCondition, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(cards);
        if (item == null || string.IsNullOrWhiteSpace(item.ItemId) ||
            string.IsNullOrWhiteSpace(item.TypeKey) || item.ActualTags == null)
            return new(LogisticsRuleDecision.Invalid, "Item facts are incomplete");
        if (network.IsMain)
            return !string.IsNullOrWhiteSpace(network.CardId)
                ? new(LogisticsRuleDecision.Invalid, "Main network cannot bind a card")
                : new(LogisticsRuleDecision.Allowed, "Main network has no rule filter");
        if (network.CardId == null)
            return new(LogisticsRuleDecision.Allowed, "Subnetwork has no card filter");
        if (!cards.TryGetValue(network.CardId, out var card))
            return new(LogisticsRuleDecision.Invalid, "Bound network card is unavailable");
        return card.Rules.Evaluate(item, isKnown);
    }

    public static int MissingAmount(int target, int present)
    {
        if (target < 0 || present < 0)
            throw new ArgumentOutOfRangeException(nameof(target),
                "Target and current quantities must be nonnegative");
        return Math.Max(0, target - present);
    }
}
