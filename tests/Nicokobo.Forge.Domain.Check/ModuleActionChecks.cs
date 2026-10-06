using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

internal static class ModuleActionChecks
{
    internal static void Run()
    {
        int checks = 0, dispatched = 0;
        const string owner = "test.actions", moduleId = owner + ".module";
        var messages = new List<string>();
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Module action readiness: " + message); }
        void Reject(Action subscribe, string message)
        {
            try { subscribe(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new Exception("Module action readiness: " + message);
        }
        void Reset()
        {
            foreach (var name in new[] { "_actionInstalled", "_actionLocalizationReady", "_actionFailed" })
                typeof(ForgeModuleApi).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, false);
            NativeHookSet.Installations.Clear(); NativeHookSet.Removals.Clear();
            NativeHookSet.InstallResult = true; messages.Clear();
            LocalizationSettings.HasSettings = true;
            LocalizationSettings.Instance.m_InitializingOperationHandle = new();
            ForgeModuleApi.Configure(true, messages.Add);
        }
        IDisposable Subscribe(string id) => ForgeModuleApi.SubscribeAction(owner, owner + "." + id,
            moduleId, _ => dispatched++);
        void Dispatch() => typeof(ForgeModuleApi).GetMethod("BeforeAction",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
                [new GameItem { identifier = moduleId }, new GameInventory(), new GameItem()]);

        Reset();
        NativeItemRegistry.Offers.Clear(); NativeItemRegistry.Outcomes.Clear();
        NativeItemRegistry.Offers.Add(new(owner, moduleId, NativeItemKind.Module,
            () => new GameItem { identifier = moduleId }, new()));
        LocalizationSettings.HasSettings = false;
        var cancelled = Subscribe("cancelled");
        Expect(NativeHookSet.Installations.Count == 0, "Registration touched native hooks before localization existed");
        cancelled.Dispose(); cancelled.Dispose(); ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 0 && NativeHookSet.Removals.Count == 0,
            "Cancelling a pending callback installed or removed a nonexistent hook");

        var first = Subscribe("first"); var second = Subscribe("second");
        ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 0, "Missing settings did not defer hooks");
        LocalizationSettings.HasSettings = true;
        LocalizationSettings.Instance.m_InitializingOperationHandle = null; ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 0, "Missing existing handle did not defer hooks");
        var operation = new Initialization { Valid = false };
        LocalizationSettings.Instance.m_InitializingOperationHandle = operation; ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 0, "Invalid existing handle did not defer hooks");
        operation.Valid = true; operation.IsDone = false; ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 0, "Incomplete localization did not defer hooks");
        Dispatch();
        Expect(dispatched == 0, "A pending hook dispatched a module action");
        first.Dispose(); operation.IsDone = true; ForgeModuleApi.UpdateActions(); ForgeModuleApi.UpdateActions();
        Expect(NativeHookSet.Installations.Count == 1, "Completed localization did not install exactly one shared hook");
        Dispatch();
        Expect(dispatched == 1, "Pending cancellation was ignored or the remaining callback was lost");
        var third = Subscribe("third");
        Expect(NativeHookSet.Installations.Count == 1, "A late callback reinstalled the shared hook");
        second.Dispose();
        Expect(NativeHookSet.Removals.Count == 0, "Removing one owner disabled another active callback");
        third.Dispose(); third.Dispose();
        Expect(NativeHookSet.Removals.Count == 1, "Final disposal did not remove the hook exactly once");
        using (var readded = Subscribe("readded"))
            Expect(NativeHookSet.Installations.Count == 2, "A callback registered after final disposal could not reinstall");

        Reset(); operation = new Initialization { Status = AsyncOperationStatus.Failed };
        LocalizationSettings.Instance.m_InitializingOperationHandle = operation;
        using (var failed = Subscribe("failed"))
        {
            ForgeModuleApi.UpdateActions(); ForgeModuleApi.UpdateActions();
            Expect(NativeHookSet.Installations.Count == 0 && messages.Count == 1,
                "Failed localization touched native hooks or repeatedly logged failure");
            operation.Status = AsyncOperationStatus.Succeeded; ForgeModuleApi.UpdateActions();
            Expect(NativeHookSet.Installations.Count == 0, "A failed adapter retried installation");
            Reject(() => Subscribe("after_failure"), "The failed adapter accepted another callback");
        }

        Reset(); NativeHookSet.InstallResult = false;
        using (var failedHook = Subscribe("failed_hook"))
        {
            ForgeModuleApi.UpdateActions(); ForgeModuleApi.UpdateActions();
            Expect(NativeHookSet.Installations.Count == 1, "A rejected native hook retried installation");
            int previous = dispatched; Dispatch();
            Expect(dispatched == previous, "A rejected native hook dispatched callbacks");
            Reject(() => Subscribe("after_hook_failure"), "A rejected hook accepted another callback");
        }
        Expect(NativeHookSet.Removals.Count == 0, "Disposing a rejected hook removed a nonexistent hook");
        Reset(); ForgeModuleApi.Configure(false, messages.Add);
        Reject(() => Subscribe("disabled"), "An unknown-build adapter accepted an action");
        Expect(NativeHookSet.Installations.Count == 0, "An unknown-build adapter touched native hooks");
        Reset(); NativeItemRegistry.Offers.Clear(); NativeItemRegistry.Outcomes.Clear();
        Console.WriteLine($"Module action readiness checks passed: {checks}.");
    }
}
