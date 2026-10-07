namespace Nicokobo.CompatibilityPatches.Patches.WagePerksStartup;

internal enum LocalizationState { Pending, Succeeded, Failed }

/// <summary>Owns only the deferred calls, not the target Mod's Harmony hooks.</summary>
internal sealed class DeferredStartupPatches
{
    // These four native static constructors call LocHelper in Build 25382790.
    internal static bool RequiresLocalization(string? targetName) => targetName is
        "Il2Cpp.ModuleEffectHelper" or "Il2Cpp.AugHelper" or
        "Il2Cpp.ItemFeatureList" or "Il2Cpp.StoreClientListTierSubstance";

    private readonly List<Action> _pending = new();
    private bool _ready, _failed;

    internal int PendingCount => _pending.Count;
    internal bool Failed => _failed;

    internal bool Intercept(Action install)
    {
        if (_ready) return true;
        if (!_failed) _pending.Add(install);
        return false;
    }

    internal int Observe(LocalizationState state)
    {
        if (_failed || _ready || state == LocalizationState.Pending) return 0;
        if (state == LocalizationState.Failed)
        {
            Disable();
            return 0;
        }
        // Replayed calls pass through the Harmony prefix to the original method.
        _ready = true;
        var pending = _pending.ToArray();
        _pending.Clear();
        try
        {
            foreach (var install in pending) install();
            return pending.Length;
        }
        catch
        {
            Disable();
            throw;
        }
    }

    internal void Disable()
    {
        _ready = false;
        _failed = true;
        _pending.Clear();
    }
}
