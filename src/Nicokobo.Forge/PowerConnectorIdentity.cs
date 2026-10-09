namespace Nicokobo.Forge;

// Native factories initialize power before assigning identifier. An unidentified
// item must retain its ordinary native path; only a registered ID is a connector.
internal static class PowerConnectorIdentity
{
    internal static T? Find<T>(IReadOnlyDictionary<string, T> providers, string? identifier) where T : class =>
        !string.IsNullOrEmpty(identifier) && providers.TryGetValue(identifier, out var provider) ? provider : null;
}
