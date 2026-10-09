namespace Il2Cpp;

// Offline regression model of the native state/modifiedState boundary.
// This checks the API against detached snapshots; it does not run IL2CPP.
public sealed partial class GameItem
{
    private TagSystem _state = new();
    public TagSystem state
    {
        get { _state.LiquidSnapshot = Liquid; return _state; }
        set { _state = value; Liquid = value.LiquidSnapshot; }
    }
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
    internal Nicokobo.Forge.ForgeMachineLiquidSnapshot? LiquidSnapshot;
    public TagSystem Clone()
    {
        var copy = new TagSystem { LiquidSnapshot = LiquidSnapshot };
        foreach (var pair in dict) copy.dict[pair.Key] = pair.Value.Clone();
        return copy;
    }
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
