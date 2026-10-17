namespace Nicokobo.Forge;

internal static class MachineNightCycle
{
    internal static (int Completed, string Status) Run(Func<int> resolveLimit, Func<string> processBatch)
    {
        int limit = resolveLimit();
        if (limit < 1) return (0, "nightly-count-invalid");
        int completed = 0;
        string status = "nightly-limit-reached";
        while (completed < limit)
        {
            if (completed > 0)
            {
                int currentLimit = resolveLimit();
                if (currentLimit < 1) return (completed, "nightly-count-invalid");
                if (completed >= currentLimit) return (completed, "nightly-limit-reached");
            }
            // Each call plans against the remaining live resources and commits
            // its own transaction. Never retry a rejected or rolled-back batch.
            status = processBatch();
            if (!status.StartsWith("produced:", StringComparison.Ordinal)) break;
            completed++;
        }
        return (completed, status);
    }
}
