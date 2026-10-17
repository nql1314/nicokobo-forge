namespace Nicokobo.Forge;

/// <summary>Ownership of a root created by one OpenForCreation call. Existing
/// or decoded roots are borrowed and are never deleted by compensation.</summary>
public sealed class ForgeInventoryEndpointCreation
{
    public ForgeInventoryEndpoint Endpoint { get; }
    public bool Created { get; }
    private readonly Func<bool> _rollback;
    private bool _rolledBack;
    internal ForgeInventoryEndpointCreation(ForgeInventoryEndpoint endpoint, bool created, Func<bool> rollback)
    { Endpoint = endpoint; Created = created; _rollback = rollback; }

    /// <summary>Delete only this call's still-empty, unchanged root in its
    /// original run. Remove owned children first. Failed rollback is retryable.</summary>
    public bool RollbackCreation()
    {
        if (!Created || _rolledBack) return true;
        if (!_rollback()) return false;
        _rolledBack = true; return true;
    }
}
