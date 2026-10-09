namespace Nicokobo.Forge;

/// <summary>The real native batch adapter uses this boundary after material
/// and output writes. A throwing readback must enter compensation and release
/// the batch just like a rejected readback.</summary>
internal static class NativeBatchFinalization
{
    internal static void DetachForDeferredDestruction(Func<bool> sameSnapshot,
        Func<bool> detached, Func<bool> expel, Action<string> reject)
    {
        try
        {
            if (!sameSnapshot()) throw new InvalidOperationException("deferred input identity or custody changed");
            if (!detached() && (!expel() || !detached()))
                throw new InvalidOperationException("deferred input could not be detached");
        }
        catch (Exception ex)
        {
            try { reject("native input destruction deferred, batch rejected: " + ex.Message); } catch { }
        }
    }

    internal static bool Complete(Func<bool> readCommit, IReadOnlyList<Func<bool>> restore,
        Action<string> fault, Action cleanup)
    {
        bool committed = false, restored = true;
        string? readError = null;
        try
        {
            try { committed = readCommit(); }
            catch (Exception ex) { readError = "native batch final readback failed: " + ex.Message; }
            if (!committed)
                foreach (var compensation in restore)
                    try { if (!compensation()) restored = false; }
                    catch { restored = false; }
            if (readError != null || !restored)
                try { fault(readError ?? "native batch rollback uncertain"); } catch { }
            return committed;
        }
        finally
        {
            try { cleanup(); }
            catch (Exception ex) { try { fault("native batch cleanup failed: " + ex.Message); } catch { } }
        }
    }
}
