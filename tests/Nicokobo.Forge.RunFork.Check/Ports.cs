using System.Reflection;
using Il2Cpp;

namespace HarmonyLib
{
    public sealed class HarmonyMethod(MethodInfo method) { public MethodInfo Method = method; public int priority; }
    public static class Priority { public const int First = 800; }
    public static class AccessTools
    { public static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)!; }
    public sealed class Harmony(string id)
    {
        public void Patch(MethodInfo target, HarmonyMethod? prefix = null, HarmonyMethod? postfix = null,
            HarmonyMethod? transpiler = null, HarmonyMethod? finalizer = null)
        { _ = id; Hooks.All[target.Name] = new(prefix?.Method, postfix?.Method, finalizer?.Method, prefix?.priority ?? 0); }
        public void UnpatchSelf() => Hooks.All.Clear();
    }
    internal sealed record Hook(MethodInfo? Prefix, MethodInfo? Postfix, MethodInfo? Finalizer, int Priority);
    internal static class Hooks
    {
        internal static readonly Dictionary<string, Hook> All = [];
        internal static object? Invoke(MethodInfo? method, params object?[] args)
        {
            try { return method?.Invoke(null, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException!; }
        }
    }
}
namespace Il2Cpp
{
    public static class SaveFiles { public static string Slot(int slot) => $"save_{slot}.es3"; }
    public static class StoreStation { public static int Day = 7; public static int GetDayCounter() => Day; }
    public sealed class PlayerStore
    {
        public static PlayerStore? instance;
        public IntPtr Pointer = (IntPtr)41;
        public string runID = "source-run";
        public int saveSlotId = 2, Allocations, startType;
        public Dictionary<string, string> modData = [];
        public readonly List<(string Run, int Slot, Dictionary<string, string> Data)> Writes = [];
        public Action? AtInitial, BeforeWrite;
        public bool FailWrite, SkipSave, SuppressWrite, CorruptWrite;
        public void StartNewGame() => HarmonyLib.Hooks.Invoke(HarmonyLib.Hooks.All[nameof(StartNewGame)].Prefix, this);
        public void InitialSave()
        {
            var hook = HarmonyLib.Hooks.All[nameof(InitialSave)];
            object?[] prefix = [this, false];
            Exception? error = null;
            try
            {
                HarmonyLib.Hooks.Invoke(hook.Prefix, prefix);
                AtInitial?.Invoke();
                Allocations++; saveSlotId++; runID = Guid.NewGuid().ToString("D");
                SaveGame();
                HarmonyLib.Hooks.Invoke(hook.Postfix, this, true);
            }
            catch (Exception ex) { error = ex; }
            error = (Exception?)HarmonyLib.Hooks.Invoke(hook.Finalizer, this, prefix[1], error);
            if (error != null) throw error;
        }
        public void SaveGame()
        {
            HarmonyLib.Hooks.Invoke(HarmonyLib.Hooks.All[nameof(SaveGame)].Prefix, this);
            if (SkipSave) return;
            WriteSlotFile();
        }
        public void WriteSlotFile()
        {
            HarmonyLib.Hooks.Invoke(HarmonyLib.Hooks.All[nameof(WriteSlotFile)].Prefix, this);
            var callback = BeforeWrite; BeforeWrite = null; callback?.Invoke();
            if (FailWrite) throw new IOException("Injected native file failure");
            Writes.Add((runID, saveSlotId, new(modData)));
            if (!SuppressWrite)
                File.WriteAllText(Path.Combine(UnityEngine.Application.persistentDataPath, SaveFiles.Slot(saveSlotId)),
                    System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
                    { ["playerStore"] = new Dictionary<string, object> { ["__type"] = "PlayerStore,Assembly-CSharp", ["value"] = new
                        { runID = CorruptWrite ? "wrong-run" : runID, saveSlotId, startType, modData } } }));
            HarmonyLib.Hooks.Invoke(HarmonyLib.Hooks.All[nameof(WriteSlotFile)].Postfix, this, true);
        }
    }
}
namespace UnityEngine
{
    public static class Application
    { public static string persistentDataPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "forge-run-fork-check-" + Guid.NewGuid().ToString("N"))).FullName; }
}
namespace Nicokobo.Forge.Runtime
{
    internal static class NativeHookSet
    {
        internal static MethodInfo Require(Type type, string name, Type[] args, Type returns) => type.GetMethod(name)!;
        internal static void RequireNativeEntryPoint(MethodInfo method) { }
        internal static void Remove(string id, Action<string>? log) => HarmonyLib.Hooks.All.Clear();
    }
}
namespace Nicokobo.Forge
{
    public sealed record ForgeRunIdentity(string RunId, int SlotId, int Day);
    public enum ForgeLifecyclePhase { AfterGameLoadedLate }
    public sealed record ForgeLifecycleContext(PlayerStore Store);
    public static class ForgeLifecycleApi
    {
        private static Action<ForgeLifecycleContext>? _loaded;
        public static IDisposable Subscribe(string owner, string id, ForgeLifecyclePhase phase, Action<ForgeLifecycleContext> handler)
        { _loaded = handler; return new Runtime.CallbackLease(() => _loaded = null); }
        internal static void Loaded(PlayerStore store) => _loaded?.Invoke(new(store));
    }
}
