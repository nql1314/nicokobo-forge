namespace Nicokobo.Forge.Runtime;

internal readonly record struct InventoryReadKey(IntPtr Store, string RunId, int SlotId);

// Only detached values survive a read. Mutation hooks invalidate immediately;
// a bounded lifetime also catches native/third-party direct field writes.
internal sealed class InventoryReadCache<T>(double maximumAgeSeconds)
{
    private InventoryReadKey _key;
    private long _revision;
    private bool _hasValue;
    private T? _value;
    private double _capturedAt;

    internal void Invalidate()
    {
        unchecked { _revision++; }
        _hasValue = false;
        _value = default;
    }

    internal T Capture(InventoryReadKey key, double now, Func<T> capture)
    {
        if (_hasValue && _key == key && now >= _capturedAt &&
            now - _capturedAt < maximumAgeSeconds) return _value!;
        // An expired/different-run value must not become reusable after a failed scan.
        _hasValue = false;
        _value = default;
        long revision = _revision;
        T value = capture();
        // A nested native callback may mutate inventory while a snapshot is captured.
        if (revision == _revision)
        {
            _key = key;
            _value = value;
            _capturedAt = now;
            _hasValue = true;
        }
        return value;
    }
}
