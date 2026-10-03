namespace Nicokobo.Forge.Runtime;

internal readonly record struct InventoryReadKey(IntPtr Store, string RunId, int SlotId, int Frame);

// Only detached values survive a read. Native handles are never retained across frames.
internal sealed class InventoryReadCache<T>
{
    private InventoryReadKey _key;
    private long _revision;
    private bool _hasValue;
    private T? _value;

    internal void Invalidate()
    {
        unchecked { _revision++; }
        _hasValue = false;
        _value = default;
    }

    internal T Capture(InventoryReadKey key, Func<T> capture)
    {
        if (_hasValue && _key == key) return _value!;
        long revision = _revision;
        T value = capture();
        // A nested native callback may mutate inventory while a snapshot is captured.
        if (revision == _revision)
        {
            _key = key;
            _value = value;
            _hasValue = true;
        }
        return value;
    }
}
