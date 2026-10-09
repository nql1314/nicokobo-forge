using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nicokobo.Forge;

internal static class ForgeTextInputFocus
{
    internal static bool ShouldSuppressHotkeys()
    {
        if (!Application.isFocused) return true;
        // Self-drawn Mod fields publish their focus check as a BCL delegate.
        // Forge can use it without requiring any of those content/tool DLLs.
        if (AppDomain.CurrentDomain.GetData("Nicokobo.Mods.SuppressTextInputHotkeys") is Func<bool> suppress && suppress())
            return true;

        // Unity objects can keep a managed wrapper after native destruction.
        // Use Unity's equality check: ?. only checks the managed reference.
        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            var selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                var tmp = selected.GetComponentInParent(Il2CppType.Of<TMP_InputField>())?.TryCast<TMP_InputField>();
                if (tmp != null && tmp.isActiveAndEnabled && tmp.isFocused) return true;
                var legacy = selected.GetComponentInParent(Il2CppType.Of<InputField>())?.TryCast<InputField>();
                if (legacy != null && legacy.isActiveAndEnabled && legacy.isFocused) return true;
            }
        }
        return !string.IsNullOrEmpty(Input.compositionString);
    }
}
