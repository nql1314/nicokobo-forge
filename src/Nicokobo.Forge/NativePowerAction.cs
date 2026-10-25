namespace Nicokobo.Forge;

// Some native callbacks ignore DrawPowerSource's return value. Reserve and
// verify at CanDraw, then require the matching Draw before committing writes.
// This receipt belongs to one synchronous native operation, never a UI query.
internal sealed class NativePowerAction(Func<int, IForgePowerTransaction?> plan) : IDisposable
{
    private IForgePowerTransaction? _receipt;
    private int? _cost;
    private bool _drawn;
    internal bool Failed { get; private set; }
    internal bool HasReservation => _cost != null;
    internal bool WasDrawn => _drawn;

    internal bool Prepare(int cost)
    {
        if (cost < 0 || _drawn) { Failed = true; return false; }
        if (Failed) return false;
        if (_cost != null)
        {
            if (_cost != cost) { Failed = true; return false; }
            return CheckReceipt();
        }
        _cost = cost;
        if (cost == 0) return true;
        try
        {
            _receipt = plan(cost);
            if (_receipt == null || !_receipt.Validate()) { Failed = true; return false; }
            _receipt.Debit();
            return CheckReceipt();
        }
        catch { Failed = true; return false; }
    }

    internal bool Draw(int cost)
    {
        if (_drawn) { Failed = true; return false; }
        if (!Prepare(cost)) return false;
        _drawn = true;
        return true;
    }

    private bool CheckReceipt()
    {
        try
        {
            if (_cost == 0 || _receipt?.Verify() == true) return true;
        }
        catch { }
        Failed = true; return false;
    }

    internal bool Verify() => !Failed && (_cost == null || _drawn && CheckReceipt());
    internal bool Restore() => _receipt?.Restore() ?? true;
    public void Dispose() => _receipt?.Dispose();
}
