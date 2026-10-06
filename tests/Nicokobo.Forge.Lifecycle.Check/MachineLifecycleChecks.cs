using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class MachineLifecycleChecks
{
    internal static void Run(Action<bool, string> require)
    {
        require(ForgeMachineHooks.Install(true, _ => { }), "Production machine lifecycle installer failed");
        var store = PlayerStore.Instance;
        store.Items.Add(new());
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        int reads = ForgeMachineUi.Reads, resets = ForgeMachineUi.Resets;
        var quarantine = (HashSet<int>)typeof(ForgeMachineRuntime).GetField("Quarantined", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        quarantine.Add(42);
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.LoadGame), store, false);
        require(ForgeMachineUi.Resets == resets && quarantine.Contains(42), "Refused LoadGame cleared live machine UI or quarantine");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        require(ForgeMachineUi.Reads == reads, "Refused LoadGame cleared the actual same-day machine attempt ledger");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.LoadGame), store, true);
        require(ForgeMachineUi.Resets == resets + 1 && quarantine.Count == 0, "Executed LoadGame did not reset machine state exactly once");
        require(ForgeMachineUi.Reads == reads + 1, "Executed LoadGame did not rebind live machine slots after reset");
        Boundary.Call(typeof(PlayerStore), nameof(PlayerStore.EndNight), store, true);
        require(ForgeMachineUi.Reads == reads + 2, "Executed LoadGame retained an earlier session's nightly attempt ledger");
    }
}
