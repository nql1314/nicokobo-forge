namespace Il2Cpp;

// Offline regression model of the native state/modifiedState boundary.
// This checks the API against detached snapshots; it does not run IL2CPP.
public sealed partial class GameItem
{
    public TagSystem state { get; } = new();
    public long unitValue;
    private readonly TagSystem _modifiedState = new();

    public void SyncModifiedState()
    {
        _modifiedState.dict.Clear();
        foreach (var entry in state.dict)
            _modifiedState.dict[entry.Key] = entry.Value.Clone();
    }

    public TagState? GetTagReadonly(string key) =>
        _modifiedState.dict.TryGetValue(key, out var tag) ? tag.Clone() : null;
}

public sealed class TagSystem
{
    public Dictionary<string, TagState> dict { get; } = new(StringComparer.Ordinal);
}

public sealed class TagState(string identifier, string identifierName)
{
    public string? valueString;
    private bool _enabled;
    public TagState Enable() { _enabled = true; return this; }
    public TagState SetString(string value) { valueString = value; return this; }
    public TagState Clone() => new(identifier, identifierName)
        { valueString = valueString, _enabled = _enabled };
}
