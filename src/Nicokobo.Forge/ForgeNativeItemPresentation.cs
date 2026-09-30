using HarmonyLib;
using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine.Localization.Settings;

namespace Nicokobo.Forge;

// Keep registered item text in the selected game language, including items
// restored from a save that was written in another language.
internal static class ForgeNativeItemPresentation
{
    private static Action<string>? _log;
    private static bool _enabled;

    internal static void Install(HarmonyLib.Harmony harmony, bool knownBuild,
        Action<string> log)
    {
        _log = log;
        if (!knownBuild) return;
        try
        {
            if (!NativeHookSet.Install("nicokobo.forge.item_presentation", [new(typeof(GameItemElement), nameof(GameItemElement.GetTooltipBasic),
                [], typeof(RichTextBuilder), typeof(ForgeNativeItemPresentation), nameof(BeforeTooltip), nameof(AfterTooltip))], log)) return;
            _enabled = true;
            log("[NicokoboForge/Tooltip] registered item localization and source footer installed");
        }
        catch (Exception ex)
        {
            log($"[ERROR] [NicokoboForge/Tooltip] disabled; {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void BeforeTooltip(GameItemElement __instance)
    {
        if (!_enabled || __instance == null || __instance.Pointer == IntPtr.Zero)
            return;
        try
        {
            if (!NativeItemRegistry.TryGetAppliedPresentation(__instance.identifier,
                    out var options, out _) || options == null)
                return;
            bool english = PreferEnglish();
            SetIfDifferent(__instance.name, options.Name, english,
                value => __instance.name = value);
            SetIfDifferent(__instance.shortDescription,
                options.ShortDescription, english,
                value => __instance.shortDescription = value);
            SetIfDifferent(__instance.flavorText, options.FlavorText, english,
                value => __instance.flavorText = value);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/Tooltip] localization failed; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AfterTooltip(GameItemElement __instance,
        RichTextBuilder __result)
    {
        if (!_enabled || __instance == null || __instance.Pointer == IntPtr.Zero ||
            __result == null || __result.Pointer == IntPtr.Zero) return;
        try
        {
            if (!NativeItemRegistry.TryGetAppliedPresentation(__instance.identifier,
                    out _, out var ownerName) || ownerName == null)
                return;
            __result.AddLine(ownerName.For(PreferEnglish()),
                color: RenderHandler.ColorPalette.Gray, italic: true);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/Tooltip] footer failed; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SetIfDifferent(string current, LocalizedItemText? text,
        bool english, Action<string> set)
    {
        if (text == null) return;
        string desired = text.For(english);
        if (current != desired) set(desired);
    }

    private static bool PreferEnglish()
    {
        try
        {
            string? code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            return code?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch { return false; }
    }
}
