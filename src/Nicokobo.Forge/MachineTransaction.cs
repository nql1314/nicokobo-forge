namespace Nicokobo.Forge;

internal sealed record MachineTransactionStep(string Name, Action Apply, Func<bool> Restore);
internal sealed record MachineTransactionResult(bool Committed, string Status, bool Restored);

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
