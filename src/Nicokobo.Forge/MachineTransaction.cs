namespace Nicokobo.Forge;

internal sealed record MachineTransactionStep(string Name, Action Apply, Func<bool> Restore);
internal sealed record MachineTransactionResult(bool Committed, string Status, bool Restored);

// A factory or resource callback can re-enter the same machine before the outer
// batch commits. Its successful debit would then be undone by the outer rollback.
// Keep the lease through planning, writes and compensation; independent machines
// remain available and an exception always releases the lease.
internal sealed class MachineBatchGate
{
    private readonly HashSet<IntPtr> _active = [];
    internal IDisposable? TryEnter(IntPtr machine)
    {
        lock (_active)
            if (!_active.Add(machine)) return null;
        return new Runtime.CallbackLease(() => { lock (_active) _active.Remove(machine); });
    }
}

// Register undo before attempting a write: a native call may mutate and then
// fail. Restore every attempted step, including after another restore fails.
internal static class MachineTransaction
{
    internal static MachineTransactionResult Run(IReadOnlyList<MachineTransactionStep> steps,
        Action<string>? log = null)
    {
        int attempted = 0;
        try
        {
            foreach (var step in steps) { attempted++; step.Apply(); }
            return new(true, "committed", true);
        }
        catch (Exception ex)
        {
            string status = $"{steps[attempted - 1].Name}:{ex.Message}";
            bool restored = true;
            void Report(string failure) { try { log?.Invoke(failure); } catch { } }
            for (int index = attempted - 1; index >= 0; index--)
                try { if (!steps[index].Restore()) { restored = false; Report(steps[index].Name); } }
                catch (Exception restore) { restored = false; Report($"{steps[index].Name}:{restore.Message}"); }
            return new(false, status, restored);
        }
    }
}
