using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

/// <summary>
/// Claims start identity and its current-build native carrier. This first API
/// does not perform menu selection or save writes.
/// </summary>
public static class ForgeStartApi
{
    private static readonly object Gate = new();
    private static readonly StartCatalog Catalog = new();
    private static Action<string>? _log;

    public static SubmitResult RegisterStart(string ownerId, string startId,
        int nativeStartType)
    {
        SubmitResult result;
        Action<string>? log;
        lock (Gate)
        {
            result = Catalog.Submit(ownerId, startId, nativeStartType);
            log = _log;
        }
        var prefix = result.Status is SubmitStatus.Accepted or SubmitStatus.AlreadyPresent
            ? "" : "[WARN] ";
        SafeLog(log, prefix + $"[NicokoboForge/Start] owner={ownerId}; id={startId}; " +
            $"nativeType={nativeStartType}; status={result.Status}; reason={result.Reason}");
        return result;
    }

    public static IReadOnlyList<StartRegistrationView> Snapshot()
    {
        lock (Gate) return Catalog.Snapshot();
    }

    internal static void SetLogger(Action<string> log)
    {
        IReadOnlyList<StartRegistrationView> staged;
        lock (Gate)
        {
            _log = log;
            staged = Catalog.Snapshot();
        }
        foreach (var start in staged)
            SafeLog(log, $"[NicokoboForge/Start] owner={start.OwnerId}; id={start.StartId}; " +
                $"nativeType={start.NativeStartType}; status=Staged; native flow caller-owned");
    }

    private static void SafeLog(Action<string>? log, string message)
    {
        try { log?.Invoke(message); }
        catch { /* Logging must not affect registration. */ }
    }
}
