using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Observed registration/application changes. Deferred gameplay setup
/// can respond to a change instead of repeatedly polling native directories.</summary>
public static class ForgeContentApi
{
    private static readonly OwnedCallbacks<bool, NativeApplicationView> Callbacks = new();
    private static Action<string>? _log;
    internal static void Configure(Action<string> log) => _log = log;
    public static IDisposable Subscribe(string ownerId, string callbackId, Action<NativeApplicationView> callback) =>
        Callbacks.Add(ownerId, callbackId, true, callback);
    internal static void Changed(NativeApplicationView view) => Callbacks.Dispatch(true, view, _log);
}
