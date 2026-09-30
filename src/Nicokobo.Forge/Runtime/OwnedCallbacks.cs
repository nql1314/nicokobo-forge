using System.Text.RegularExpressions;

namespace Nicokobo.Forge.Runtime;

// Independent of Harmony, IL2CPP and game singletons. Dispatch uses a stable
// snapshot; a failing subscriber cannot prevent the next owner from running.
internal sealed class OwnedCallbacks<TSignal, TContext> where TSignal : notnull
{
    private sealed record Entry(string Owner, string Id, TSignal Signal,
        int Order, Action<TContext> Callback);
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
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
        lock (_gate)
        {
            if (_entries.ContainsKey(id)) throw new InvalidOperationException($"Callback ID already registered: {id}");
            _entries.Add(id, new(owner, id, signal, order, callback));
        }
        return new CallbackLease(() => { lock (_gate) _entries.Remove(id); });
    }
    internal bool Has(TSignal signal)
    { lock (_gate) return _entries.Values.Any(e => EqualityComparer<TSignal>.Default.Equals(e.Signal, signal)); }

    internal void Dispatch(TSignal signal, TContext context, Action<string>? log = null)
    {
        Entry[] entries;
        lock (_gate) entries = _entries.Values
            .Where(e => EqualityComparer<TSignal>.Default.Equals(e.Signal, signal))
            .OrderBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
        foreach (var entry in entries)
        {
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
