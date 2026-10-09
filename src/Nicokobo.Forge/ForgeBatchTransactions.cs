using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Extra content-owned state committed with one actual machine batch.
/// Planning and Validate must only read state. Restore compensates this batch
/// only, and must verify the original state even after Apply throws.</summary>
public interface IForgeBatchTransaction : IDisposable
{
    bool Validate();
    void Apply(IReadOnlyList<GameItem> products);
    bool Verify();
    bool Restore();
    void Fault(string reason);
}

/// <summary>A frozen external power receipt. Never persists a virtual balance.
/// The machine runtime owns its lifetime through resource commit/rollback.</summary>
public interface IForgePowerTransaction : IDisposable
{
    bool Validate();
    void Debit();
    bool Verify();
    bool Restore();
    void Fault(string reason);
}

public sealed record ForgeTransactionStep(string Name, Action Apply, Func<bool> Restore);
public sealed record ForgeTransactionResult(bool Committed, bool Restored, string Status);

/// <summary>Register compensation before writes, attempt every compensation,
/// and report an uncertain result instead of replaying a partial transaction.</summary>
public static class ForgeTransactionApi
{
    public static ForgeTransactionResult Run(IReadOnlyList<ForgeTransactionStep> steps)
    {
        if (steps == null || steps.Count == 0 || steps.Any(step => step == null ||
            string.IsNullOrWhiteSpace(step.Name) || step.Apply == null || step.Restore == null))
            throw new ArgumentException("A nonempty transaction is required");
        var result = MachineTransaction.Run(steps.Select(step =>
            new MachineTransactionStep(step.Name, step.Apply, step.Restore)).ToArray());
        return new(result.Committed, result.Restored, result.Status);
    }
}

public sealed record ForgePowerSourceAmount(int InstanceId, int Energy);
public sealed record ForgePowerSourceDebit(int InstanceId, int Before, int Debit);

/// <summary>Absolute remaining energy, descending; a stable ID breaks ties.
/// Insufficient total energy produces no partial plan.</summary>
public static class ForgePowerBatchMath
{
    public static IReadOnlyList<ForgePowerSourceDebit>? Plan(IEnumerable<ForgePowerSourceAmount> sources, int cost)
    {
        if (sources == null || cost < 0) return null;
        var snapshot = sources.ToArray();
        if (snapshot.Any(source => source.InstanceId <= 0 || source.Energy < 0) ||
            snapshot.Select(source => source.InstanceId).Distinct().Count() != snapshot.Length ||
            snapshot.Sum(source => (long)source.Energy) < cost) return null;
        int remaining = cost;
        var result = new List<ForgePowerSourceDebit>();
        foreach (var source in snapshot.OrderByDescending(source => source.Energy).ThenBy(source => source.InstanceId))
        {
            if (remaining == 0) break;
            int debit = Math.Min(source.Energy, remaining);
            if (debit == 0) continue;
            result.Add(new(source.InstanceId, source.Energy, debit));
            remaining -= debit;
        }
        return result.AsReadOnly();
    }
}
