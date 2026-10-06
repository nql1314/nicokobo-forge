using Il2Cpp;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;
using UnityEngine.Localization.Settings;

namespace Nicokobo.Forge;

public sealed record ForgeModuleText(string? AdditionalEffect = null, IReadOnlyList<string>? Lines = null);

/// <summary>Localized text and owned tooltip contributions. Content callbacks
/// return text; the adapter controls native rendering and ownership checks.</summary>
public static class ForgePresentationApi
{
    private static readonly Dictionary<string, LocalizedItemText> Owners = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Func<GameItem, bool, ForgeModuleText>> Modules = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (LocalizedItemText Name, LocalizedItemText Description)> Effects = new(StringComparer.Ordinal);
    private static bool _allowed, _installed;
    private static Action<string>? _log;
    internal static void Configure(bool allowed, Action<string> log) { _allowed = allowed; _log = log; }
    public static void RegisterOwnerDisplayName(string ownerId, string chinese, string english)
    {
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, ownerId + ".display") ||
            string.IsNullOrWhiteSpace(chinese) || string.IsNullOrWhiteSpace(english)) throw new ArgumentException("Owner and localized name are required");
        var text = new LocalizedItemText(chinese, english);
        if (Owners.TryGetValue(ownerId, out var old) && old != text) throw new InvalidOperationException("Owner name already registered");
        Owners[ownerId] = text;
    }
    /// <summary>Read registered presentation without invoking an item factory.</summary>
    public static string? GetRegisteredItemName(string itemId, bool english = false) =>
        NativeItemRegistry.TryGetAppliedPresentation(itemId, out var options, out _) ? options?.Name?.For(english) : null;
    /// <summary>Read the applied item's description without creating an item or running its callbacks.</summary>
    public static string? GetRegisteredItemDescription(string itemId, bool english = false) =>
        NativeItemRegistry.TryGetAppliedPresentation(itemId, out var options, out _) ? options?.ShortDescription?.For(english) : null;
    public static void RegisterModuleText(string ownerId, string itemId, Func<GameItem, bool, ForgeModuleText> text)
    {
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, itemId) || text == null) throw new ArgumentException("Owned item ID and text callback required");
        EnsureInstalled();
        if (!Modules.TryAdd(itemId, text)) throw new InvalidOperationException("Module text already registered: " + itemId);
    }
    public static void RegisterEffectText(string ownerId, string effectId, LocalizedItemText name, LocalizedItemText description)
    {
        if (!OwnedCallbacks<bool, bool>.ValidId(ownerId, effectId) || name == null || description == null) throw new ArgumentException("Owned effect ID and localized text required");
        EnsureInstalled();
        if (!Effects.TryAdd(effectId, (name, description))) throw new InvalidOperationException("Effect text already registered: " + effectId);
    }
    internal static LocalizedItemText? OwnerName(string id) => Owners.GetValueOrDefault(id);
    internal static bool PreferEnglish()
    {
        try { return LocalizationSettings.SelectedLocale?.Identifier.Code?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true; }
        catch { return false; }
    }
    private static void EnsureInstalled()
    {
        if (!_allowed) throw new InvalidOperationException("Module presentation adapter unavailable");
        if (_installed) return;
        _installed = NativeHookSet.Install("nicokobo.forge.module_presentation", [new(typeof(ModuleHelper), nameof(ModuleHelper.CreateModuleTooltip),
            [typeof(RichTextBuilder), typeof(GameItem)], typeof(void), typeof(ForgePresentationApi), nameof(BeforeModule), nameof(AfterModule))], _log);
        if (!_installed) throw new InvalidOperationException("Module presentation hook unavailable");
    }
    private static void BeforeModule(GameItem item, out ForgeModuleText? __state)
    {
        __state = null;
        if (!_installed) return;
        try
        {
            bool english = PreferEnglish();
            foreach (var (id, text) in Effects)
            {
                if (!NativeEffectRegistry.TryGetApplied(id, out var effect)) continue;
                string name = text.Name.For(english), description = text.Description.For(english);
                if (effect!.displayName != name) effect.displayName = name;
                if (effect.description != description) effect.description = description;
            }
            if (item == null || item.Pointer == IntPtr.Zero || !NativeItemRegistry.IsAppliedItem(item.identifier) ||
                !Modules.TryGetValue(item.identifier, out var callback)) return;
            var returned = callback(item, english);
            __state = returned with { Lines = returned.Lines?.ToArray() };
            var tag = item.GetTagReadonly("ADDITIONAL_EFFECT_STRING");
            if (returned.AdditionalEffect != null && tag != null && tag.valueString != returned.AdditionalEffect)
                tag.SetString(returned.AdditionalEffect);
        }
        catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Presentation] text unavailable: {ex.Message}"); } catch { } }
    }
    private static void AfterModule(RichTextBuilder builder, ForgeModuleText? __state)
    {
        if (!_installed || builder == null || builder.Pointer == IntPtr.Zero || __state?.Lines == null) return;
        try { foreach (var line in __state.Lines) builder.AddLine(line, color: RenderHandler.ColorPalette.White); }
        catch (Exception ex) { try { _log?.Invoke($"[WARN] [NicokoboForge/Presentation] tooltip rendering: {ex.Message}"); } catch { } }
    }
}
