using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge.Runtime;
using UnityEngine;

internal static class SaveListChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var logs = new List<string>();
        SaveListRuntime.Install(true, logs.Add);
        var open = NativeHookSet.Installed.Single(h => h.Method == "OnLoadGameClick");
        var icon = NativeHookSet.Installed.Single(h => h.Method == "GetIcon");
        object? Invoke(string name, object?[] args) => typeof(SaveListRuntime)
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
        object Begin()
        {
            object?[] args = [null];
            Invoke(open.Prefix!, args);
            return args[0]!;
        }
        Exception? End(MainMenuUIController menu, object state, Exception? error) =>
            (Exception?)Invoke(open.Finalizer!, [menu, state, error]);
        (Exception? Error, Sprite? Image) Read(Exception? error, Sprite? image = null)
        {
            object?[] args = [(NewGameData.StartType)55, image, error];
            return ((Exception?)Invoke(icon.Finalizer!, args), (Sprite?)args[1]);
        }
        var failure = new NullReferenceException("Removed Mod icon");
        Check(ReferenceEquals(Read(failure).Error, failure), "Icon errors outside the menu were suppressed");
        var menu = new MainMenuUIController();
        var scope = Begin();
        var valid = new Sprite();
        Check(ReferenceEquals(Read(null, valid).Image, valid), "Valid icon changed");
        var result = Read(failure);
        Check(result.Error == null && result.Image != null, "Preview icon exception did not recover");
        Check(logs.Any(s => s.Contains("startType=55") && s.Contains("Removed Mod icon")), "Lost icon error context");
        var inner = Begin();
        Check(End(menu, inner, null) == null && Read(failure).Error == null, "Nested scope lost its parent");
        Check(End(menu, scope, null) == null && menu.saveUIManager.CloseCalls == 0, "Successful list closed");
        Check(ReferenceEquals(Read(failure).Error, failure), "Successful menu leaked recovery scope");

        scope = Begin();
        Check(End(menu, scope, failure) == null, "Recoverable list error escaped");
        Check(!menu.saveUIManager.Visible && menu.confirmPanelUIManager.Message!.Contains("无法打开"),
            "Failed list left partial UI or no notice");
        End(menu, scope, null);
        Check(menu.saveUIManager.CloseCalls == 1, "Repeated finalizer closed menu twice");
        Check(ReferenceEquals(Read(failure).Error, failure), "Failed menu leaked recovery scope");

        menu = new();
        menu.saveUIManager.ThrowOnClose = true;
        Check(ReferenceEquals(End(menu, Begin(), failure), failure), "Cleanup failure swallowed original exception");
        menu = new();
        menu.confirmPanelUIManager.ThrowOnOpen = true;
        Check(ReferenceEquals(End(menu, Begin(), failure), failure), "Notice failure swallowed original exception");
        menu = new() { Destroyed = true };
        Check(ReferenceEquals(End(menu, Begin(), failure), failure), "Destroyed menu swallowed original exception");

        foreach (var fatal in new Exception[] { new AccessViolationException(), new OutOfMemoryException(),
            new StackOverflowException(), new System.Runtime.InteropServices.SEHException() })
        {
            menu = new();
            scope = Begin();
            Check(ReferenceEquals(Read(fatal).Error, fatal), "Fatal icon failure was suppressed");
            Check(ReferenceEquals(End(menu, scope, fatal), fatal) && menu.saveUIManager.CloseCalls == 0,
                "Fatal list failure was suppressed or recovery attempted");
            Check(ReferenceEquals(Read(failure).Error, failure), "Fatal path leaked recovery scope");
        }
        SaveListRuntime.Uninstall();
        Check(NativeHookSet.Installed.Count == 0, "Menu hooks survived uninstall");
        SaveListRuntime.Install(false, logs.Add);
        Check(NativeHookSet.Installed.Count == 0, "Unknown build installed menu hooks");
        NativeHookSet.InstallResult = false;
        SaveListRuntime.Install(true, logs.Add);
        Check(NativeHookSet.Installed.Count == 0, "Failed install left menu hooks");
        NativeHookSet.InstallResult = true;
        bool breakLog = false;
        SaveListRuntime.Install(true, _ => { if (breakLog) throw new InvalidOperationException("Logger failed"); });
        breakLog = true;
        scope = Begin();
        Check(Read(failure).Error == null, "Logger failure broke icon recovery");
        Check(End(new MainMenuUIController(), scope, failure) == null, "Logger failure broke menu recovery");
        SaveListRuntime.Uninstall();
        Console.WriteLine($"Save list recovery checks passed: {checks}.");
    }
}
