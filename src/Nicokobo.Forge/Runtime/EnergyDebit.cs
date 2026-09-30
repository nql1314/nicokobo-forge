namespace Nicokobo.Forge.Runtime;

public enum ForgeEnergyDebitStatus { Committed, Insufficient, Invalid, Restored, Faulted }
public sealed record ForgeEnergyDebitResult(ForgeEnergyDebitStatus Status, int Before, int After);

// Native adapters only provide observation, debit and compensation. Arithmetic,
// validation and fault decisions do not require the game's power helpers.
internal static class EnergyDebit
{
    internal static ForgeEnergyDebitResult Execute(int cost, Func<int> read,
        Func<int, int> debit, Func<int, bool> restore)
    {
        if (cost < 0) return new(ForgeEnergyDebitStatus.Invalid, 0, 0);
        int before;
        try { before = read(); } catch { return new(ForgeEnergyDebitStatus.Invalid, 0, 0); }
        if (before < 0) return new(ForgeEnergyDebitStatus.Invalid, before, before);
        if (before < cost) return new(ForgeEnergyDebitStatus.Insufficient, before, before);
        if (cost == 0) return new(ForgeEnergyDebitStatus.Committed, before, before);
        try
        {
            int removed = debit(cost), after = read();
            if (removed == cost && after == before - cost) return new(ForgeEnergyDebitStatus.Committed, before, after);
        }
        catch { }
        try
        {
            if (read() == before || restore(before) && read() == before)
                return new(ForgeEnergyDebitStatus.Restored, before, before);
        }
        catch { }
        int remaining;
        try { remaining = read(); } catch { remaining = -1; }
        return new(ForgeEnergyDebitStatus.Faulted, before, remaining);
    }
}
