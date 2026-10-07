using System.Text.Json;

namespace Nicokobo.Forge;

// Only earlier successful Forge registrations recorded in a normal save are
// eligible for cleanup. Third-party/native IDs without this provenance are kept.
internal static class MissingItemSavePolicy
{
    internal static bool ConfirmedMissing(string id, string itemType, bool registered,
        IReadOnlyDictionary<string, string> savedProviders, IReadOnlySet<string> currentDeclarations,
        IReadOnlySet<string> installedAssemblies, bool installationReadable)
    {
        if (registered || !installationReadable || currentDeclarations.Contains(id)) return false;
        // Forge's item registration APIs do not register character factories.
        if (itemType != "GameItem" && itemType != "Il2Cpp.GameItem") return false;
        return savedProviders.TryGetValue(id, out string? provider) &&
            !string.IsNullOrWhiteSpace(provider) && !installedAssemblies.Contains(provider);
    }

    internal static Dictionary<string, string> ReadProviders(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (json == null) return result;
        using var document = ForgeSaveReadbackApi.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid saved item provider map");
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name) || property.Value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(property.Value.GetString()) ||
                !result.TryAdd(property.Name, property.Value.GetString()!))
                throw new InvalidDataException("Invalid or duplicate saved item provider");
        }
        return result;
    }
}
