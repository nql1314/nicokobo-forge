using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>
/// Public entry point for declarations. This P0 API stores and validates metadata;
/// it does not yet create native game content.
/// </summary>
public static class ForgeApi
{
    private static readonly DeclarationCatalog Catalog = new();
    private static readonly object Gate = new();
    private static Action<string>? _log;

    public static SubmitResult Register(string ownerId, string version,
        Action<RegistrationBatch> declare)
    {
        ArgumentNullException.ThrowIfNull(declare);
        var batch = new RegistrationBatch(ownerId, version);
        try
        {
            declare(batch);
        }
        catch (Exception ex)
        {
            return new SubmitResult(SubmitStatus.Invalid,
                $"Declaration callback failed: {ex.GetType().Name}: {ex.Message}");
        }
        SubmitResult result;
        IReadOnlyList<RegistrationView> snapshot;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.Submit(batch.Freeze());
            snapshot = Catalog.Snapshot();
            log = _log;
        }
        try
        {
            var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
                ? "" : "[WARN] ";
            log?.Invoke(prefix + $"[NicokoboForge/Registration] owner={ownerId}; status={result.Status}; reason={result.Reason}");
            foreach (var entry in snapshot)
                log?.Invoke($"[NicokoboForge/Registration] owner={entry.OwnerId}; stage={entry.Status}; reason={entry.Reason}");
        }
        catch { /* Logging must not reject a validated declaration. */ }
        return result;
    }

    public static IReadOnlyList<RegistrationView> Snapshot()
    {
        lock (Gate) return Catalog.Snapshot();
    }

    internal static void SetLogger(Action<string> log)
    {
        IReadOnlyList<RegistrationView> snapshot;
        lock (Gate)
        {
            _log = log;
            snapshot = Catalog.Snapshot();
        }
        foreach (var entry in snapshot)
            log($"[NicokoboForge/Registration] owner={entry.OwnerId}; stage={entry.Status}; reason={entry.Reason}");
    }
}
