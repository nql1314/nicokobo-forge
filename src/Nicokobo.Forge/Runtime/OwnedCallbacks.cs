using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Runtime;

// Independent of Harmony, IL2CPP and game singletons. Dispatch uses a stable
// snapshot; a failing subscriber cannot prevent the next owner from running.
internal sealed class OwnedCallbacks<TSignal, TContext> where TSignal : notnull
{
    private sealed record Entry(string Owner, string Id, TSignal Signal,
        int Order, Action<TContext> Callback)
    {
        internal volatile bool Active = true;
    }
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<TSignal, Entry[]> _snapshots = new();
    internal int Count { get { lock (_gate) return _entries.Count; } }
    internal static bool ValidId(string owner, string id) =>
        !string.IsNullOrWhiteSpace(owner) &&
        Regex.IsMatch(owner, "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$") &&
        !string.IsNullOrWhiteSpace(id) && id.StartsWith(owner + ".", StringComparison.Ordinal) &&
        Regex.IsMatch(id, "^[a-z][a-z0-9]*(\\.[a-z][a-z0-9_]*)+$");

    internal IDisposable Add(string owner, string id, TSignal signal,
        Action<TContext> callback, int order = 0)
    {
        if (!ValidId(owner, id) || callback == null)
            throw new ArgumentException("A namespaced owner, owned callback ID and callback are required");
        var entry = new Entry(owner, id, signal, order, callback);
        lock (_gate)
        {
            if (_entries.ContainsKey(id)) throw new InvalidOperationException($"Callback ID already registered: {id}");
            _entries.Add(id, entry);
            Rebuild(signal);
        }
        return new CallbackLease(() =>
        {
            lock (_gate)
            {
                entry.Active = false;
                _entries.Remove(id);
                Rebuild(signal);
            }
        });
    }
    internal bool Has(TSignal signal)
    { lock (_gate) return _snapshots.ContainsKey(signal); }

    // Registration changes are rare; observations and module calculations are
    // frequent. Sort once per change, and leave in-flight snapshots untouched.
    private void Rebuild(TSignal signal)
    {
        var entries = _entries.Values
            .Where(e => EqualityComparer<TSignal>.Default.Equals(e.Signal, signal))
            .OrderBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        if (entries.Length == 0) _snapshots.Remove(signal);
        else _snapshots[signal] = entries;
    }

    internal void Dispatch(TSignal signal, TContext context, Action<string>? log = null)
    {
        Entry[] entries;
        lock (_gate) entries = _snapshots.GetValueOrDefault(signal) ?? [];
        foreach (var entry in entries)
        {
            // Another subscriber may have disabled this feature during this
            // dispatch. Its old snapshot must not invoke the released callback.
            if (!entry.Active) continue;
            try { entry.Callback(context); }
            catch (Exception ex)
            {
                try { log?.Invoke($"[WARN] [NicokoboForge/Callbacks] owner={entry.Owner}; id={entry.Id}; {ex.GetType().Name}: {ex.Message}"); }
                catch { }
            }
        }
    }
}

internal sealed class CallbackLease(Action release) : IDisposable
{
    private Action? _release = release;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
