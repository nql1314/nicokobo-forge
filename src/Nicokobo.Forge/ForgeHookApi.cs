using System.Reflection;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

/// <summary>Preflight for content-owned IL2CPP hooks. Call only after the loader
/// initializes IL2CPP, on the game thread. This does not install a patch or verify
/// callback signatures, game-build compatibility, hook ordering or game behavior.</summary>
public static class ForgeHookApi
{
    /// <summary>Reject abstract/open-generic wrappers, missing metadata and zero
    /// executable addresses before passing a target to Harmony. Validate every
    /// required target before patching a feature; retain ownership of its rollback.</summary>
    public static bool TryValidateNativeTarget(MethodInfo? method, out string? reason)
    {
        if (method == null) { reason = "Native hook target is missing"; return false; }
        try
        {
            NativeHookSet.RequireNativeEntryPoint(method);
            reason = null;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"{method.DeclaringType?.FullName}.{method.Name}: {ex.Message}";
            return false;
        }
    }
}
