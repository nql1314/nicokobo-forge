using Il2CppTMPro;
using Nicokobo.Forge;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

static class TextInputFocusChecks
{
    internal static void Run()
    {
        const string focusKey = "Nicokobo.Mods.SuppressTextInputHotkeys";
        var previous = AppDomain.CurrentDomain.GetData(focusKey);
        int checks = 0;
        void Expect(bool expected, string reason)
        {
            if (ForgeTextInputFocus.ShouldSuppressHotkeys() != expected)
                throw new Exception(reason);
            checks++;
        }
        try
        {
            AppDomain.CurrentDomain.SetData(focusKey, null);
            Expect(false, "Missing EventSystem blocked hotkeys");
            EventSystem.current = new() { Destroyed = true };
            Expect(false, "Destroyed EventSystem was dereferenced");
            var events = new EventSystem();
            EventSystem.current = events;
            Expect(false, "Empty selection blocked hotkeys");
            events.Selected = new() { Destroyed = true };
            Expect(false, "Destroyed selection was dereferenced");
            Input.compositionString = "输入";
            Expect(true, "Destroyed selection bypassed IME composition");
            Input.compositionString = "";
            var tmp = new TMP_InputField { isFocused = true };
            events.Selected = new() { Field = tmp };
            Expect(true, "Focused TMP input did not suppress hotkeys");
            AppDomain.CurrentDomain.SetData(focusKey, (Func<bool>)(() => false));
            Expect(true, "Unfocused Mod input bypassed focused native TMP input");
            var legacy = new InputField { isFocused = true };
            events.Selected.Field = legacy;
            Expect(true, "Unfocused Mod input bypassed focused native legacy input");
            legacy.isFocused = false;
            Expect(false, "Unfocused native input blocked hotkeys");
            tmp.isActiveAndEnabled = false;
            events.Selected.Field = tmp;
            Expect(false, "Disabled native input blocked hotkeys");
            AppDomain.CurrentDomain.SetData(focusKey, (Func<bool>)(() => true));
            Expect(true, "Focused Mod input did not suppress hotkeys");
            AppDomain.CurrentDomain.SetData(focusKey, null);
            Application.isFocused = false;
            Expect(true, "Unfocused application allowed hotkeys");
            Console.WriteLine($"Text input focus: {checks} assertions passed (destroyed Unity wrappers, native/Mod fields, IME).");
        }
        finally
        {
            AppDomain.CurrentDomain.SetData(focusKey, previous);
            EventSystem.current = null;
            Application.isFocused = true;
            Input.compositionString = "";
        }
    }
}
