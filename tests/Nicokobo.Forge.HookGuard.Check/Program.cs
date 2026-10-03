using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Startup;
using Nicokobo.Forge.Runtime;
using Nicokobo.Forge;

// Standalone fixtures only: no game classes, native detour provider, or game startup.
Il2CppInteropRuntime.Create(new RuntimeConfiguration { UnityVersion = new Version(2021, 3, 45) });
var valid = UnityVersionHandler.NewMethod();
var missingEntry = UnityVersionHandler.NewMethod();
valid.MethodPointer = new IntPtr(1); // A sentinel; it must never be passed to a detour.
missingEntry.MethodPointer = IntPtr.Zero;
Fixture.NativeMethodInfoPtr_Valid = valid.Pointer;
Fixture.NativeMethodInfoPtr_MissingEntry = missingEntry.Pointer;
var checks = 0;
try
{
    var guard = typeof(NativeHookSet).GetMethod("RequireNativeEntryPoint", BindingFlags.NonPublic | BindingFlags.Static)!;
    CheckTarget(guard, typeof(Fixture).GetMethod(nameof(Fixture.Valid))!, null);
    CheckTarget(guard, typeof(Fixture).GetMethod(nameof(Fixture.MissingEntry))!, "executable entry point");
    CheckTarget(guard, typeof(Fixture).GetMethod(nameof(Fixture.MissingMetadata))!, "metadata pointer");
    CheckTarget(guard, typeof(Fixture).GetMethod(nameof(Fixture.Managed))!, "generated method metadata");
    CheckTarget(guard, typeof(AbstractFixture).GetMethod(nameof(AbstractFixture.Abstract))!, "abstract");
    CheckTarget(guard, typeof(Fixture).GetMethod(nameof(Fixture.Generic))!, "open generic");
    Expect(!ForgeHookApi.TryValidateNativeTarget(null, out var missing) && missing != null,
        "Public guard accepted a missing method");
    Expect(ForgeHookApi.TryValidateNativeTarget(typeof(Fixture).GetMethod(nameof(Fixture.Valid)), out var validReason) && validReason == null,
        "Public guard rejected valid synthetic metadata");
    Expect(!ForgeHookApi.TryValidateNativeTarget(typeof(Fixture).GetMethod(nameof(Fixture.MissingEntry)), out var invalidReason) &&
        invalidReason != null && invalidReason.Contains(nameof(Fixture.MissingEntry)),
        "Public guard accepted a zero entry or omitted target diagnostics");

    // The first fixture has a nonzero sentinel entry. A later invalid target must
    // reject the whole batch before Harmony sees that first target.
    const string id = "nicokobo.forge.check.zero_entry";
    var messages = new List<string>();
    NativeHook Hook(string method) => new(typeof(Fixture), method, [], typeof(void),
        typeof(Fixture), Postfix: nameof(Fixture.Callback));
    bool installed = NativeHookSet.Install(id,
        [Hook(nameof(Fixture.Valid)), Hook(nameof(Fixture.MissingEntry))], messages.Add);
    Expect(!installed, "Zero native entry was accepted");
    Expect(messages.Any(x => x.Contains(nameof(Fixture.MissingEntry)) && x.Contains("executable entry point")),
        "Failure did not identify the invalid target");
    Console.WriteLine($"Native hook guard checks passed: {checks}; standalone metadata fixtures only.");
}
finally
{
    Marshal.FreeHGlobal(valid.Pointer);
    Marshal.FreeHGlobal(missingEntry.Pointer);
}

void Expect(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}

void CheckTarget(MethodInfo guard, MethodInfo target, string? error)
{
    try
    {
        guard.Invoke(null, [target]);
        Expect(error == null, $"Invalid target {target.Name} passed preflight");
    }
    catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
    {
        Expect(error != null && ex.InnerException.Message.Contains(error),
            $"Unexpected rejection for {target.Name}: {ex.InnerException.Message}");
    }
}

public abstract class AbstractFixture
{
    public abstract void Abstract();
}

public static class Fixture
{
    public static IntPtr NativeMethodInfoPtr_Valid, NativeMethodInfoPtr_MissingEntry, NativeMethodInfoPtr_MissingMetadata;
    public static void Valid() => GC.KeepAlive(NativeMethodInfoPtr_Valid);
    public static void MissingEntry() => GC.KeepAlive(NativeMethodInfoPtr_MissingEntry);
    public static void MissingMetadata() => GC.KeepAlive(NativeMethodInfoPtr_MissingMetadata);
    public static void Managed() { }
    public static void Generic<T>() { }
    public static void Callback() { }
}
