using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class PresentationChecks
{
    private const string Tag = "ADDITIONAL_EFFECT_STRING";
    private const string Owner = "nicokobo.presentation.check";
    private const string ItemId = Owner + ".module";

    internal static void Run(Action<bool, string> require)
    {
        bool held = true;
        ForgePresentationApi.Configure(true, _ => { });
        NativeItemRegistry.Applied.Add(ItemId);
        ForgePresentationApi.RegisterModuleText(Owner, ItemId, (_, english) =>
            new(english ? $"Cap +{(held ? 48 : 32)}%" : $"上限 +{(held ? 48 : 32)}%", ["Extra line"]));
        var item = Item();
        item.GetTagReadonly(Tag)!.SetString("edited copy");
        require(item.GetTagReadonly(Tag)!.valueString == "旧上限 +32%",
            "The fixture must preserve native GetTagReadonly clone semantics");
        var hook = NativeHookSet.Installed["nicokobo.forge.module_presentation"].Single();
        require(hook.Finalizer != null, "Presentation must restore transient text when native rendering throws");

        var rendered = Render(item);
        require(rendered.Lines.SequenceEqual(new[] { "Stats", " -上限 +48%", "Effects", "Extra line" }),
            "Native rendering must read the updated description once in its original effect-block position");
        require(item.state.dict[Tag].valueString == "base description" &&
            item.modifiedState!.dict[Tag].valueString == "旧上限 +32%",
            "Drawing must restore modified text without altering saved base tags");
        held = false;
        require(Render(item).Lines.Contains(" -上限 +32%"), "Ownership loss must update the next draw instead of retaining a cached cap");
        held = true;
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = new("en");
        require(Render(item).Lines.Contains(" -Cap +48%"), "The real text callback must receive the current tooltip language");
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = new("zh");

        ModuleHelper.DuringTooltip = (_, _) => throw new InvalidOperationException("native rendering failed");
        bool failed = false;
        try { Render(item); }
        catch (InvalidOperationException) { failed = true; }
        finally { ModuleHelper.DuringTooltip = null; }
        require(failed && item.modifiedState!.dict[Tag].valueString == "旧上限 +32%",
            "A native rendering exception must retain its failure and restore the original description");
        Render(item, runOriginal: false);
        require(item.modifiedState!.dict[Tag].valueString == "旧上限 +32%",
            "A foreign prefix rejecting the native body must still restore presentation state");

        bool nestedRestored = false;
        ModuleHelper.DuringTooltip = (_, _) =>
        {
            ModuleHelper.DuringTooltip = null;
            held = false;
            var inner = Render(item);
            nestedRestored = inner.Lines.Contains(" -上限 +32%") && item.GetTagReadonly(Tag)!.valueString == "上限 +48%";
            held = true;
        };
        var outer = Render(item);
        require(nestedRestored && outer.Lines.Contains(" -上限 +48%") &&
            item.modifiedState!.dict[Tag].valueString == "旧上限 +32%",
            "Nested draws must restore the outer description before restoring the original tag");

        ModuleHelper.DuringTooltip = (_, _) => item.modifiedState!.dict[Tag].SetString("foreign update");
        Render(item); ModuleHelper.DuringTooltip = null;
        require(item.modifiedState!.dict[Tag].valueString == "foreign update",
            "Restoration must preserve another mod's edit made during the draw");
        item.modifiedState.dict[Tag].SetString("旧上限 +32%");
        NativeItemRegistry.Applied.Remove(ItemId);
        require(Render(item).Lines.SequenceEqual(new[] { "Stats", " -旧上限 +32%", "Effects" }),
            "Registered but unapplied content must keep the native description");
        NativeItemRegistry.Applied.Add(ItemId);
        var foreign = Item(); foreign.identifier = "foreign.module";
        require(Render(foreign).Lines.Contains(" -旧上限 +32%") && !Render(foreign).Lines.Contains("Extra line"),
            "Foreign module text must remain unchanged");
        var missingTag = Item(); missingTag.modifiedState!.dict.Remove(Tag);
        require(Render(missingTag).Lines.SequenceEqual(new[] { "Stats", "Effects", "Extra line" }) &&
            !missingTag.modifiedState.dict.ContainsKey(Tag), "Rendering must not create missing native tags");
        var nullTextId = Owner + ".lines_only";
        NativeItemRegistry.Applied.Add(nullTextId);
        ForgePresentationApi.RegisterModuleText(Owner, nullTextId, (_, _) => new(Lines: ["Only line"]));
        var linesOnly = Item(); linesOnly.identifier = nullTextId;
        require(Render(linesOnly).Lines.SequenceEqual(new[] { "Stats", " -旧上限 +32%", "Effects", "Only line" }),
            "A lines-only contribution must preserve the existing native additional effect");
    }

    private static GameItem Item() => new()
    {
        identifier = ItemId,
        state = new() { dict = { [Tag] = new(Tag, Tag) { valueString = "base description", enabled = true } } },
        modifiedState = new() { dict = { [Tag] = new(Tag, Tag) { valueString = "旧上限 +32%", enabled = true } } }
    };

    private static RichTextBuilder Render(GameItem item, bool runOriginal = true)
    {
        var hook = NativeHookSet.Installed["nicokobo.forge.module_presentation"].Single();
        MethodInfo Method(string name) => hook.CallbackType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] arguments = [item, null];
        Method(hook.Prefix!).Invoke(null, arguments);
        var builder = new RichTextBuilder();
        try
        {
            if (runOriginal) ModuleHelper.CreateModuleTooltip(builder, item);
            Method(hook.Postfix!).Invoke(null, [builder, arguments[1]]);
            return builder;
        }
        finally { Method(hook.Finalizer!).Invoke(null, [arguments[1]]); }
    }
}

namespace Il2Cpp
{
    public sealed class RichTextBuilder
    {
        public IntPtr Pointer = (IntPtr)45;
        public readonly List<string> Lines = [];
        public void AddLine(string text, RenderHandler.ColorPalette color = RenderHandler.ColorPalette.White) => Lines.Add(text);
    }
    public static class RenderHandler { public enum ColorPalette { White, Cyan } }
    public static class ModuleHelper
    {
        public static Action<RichTextBuilder, GameItem>? DuringTooltip;
        public static void CreateModuleTooltip(RichTextBuilder builder, GameItem item)
        {
            builder.AddLine("Stats");
            if (item.IsTag("ADDITIONAL_EFFECT_STRING"))
                builder.AddLine(" -" + item.GetTagReadonly("ADDITIONAL_EFFECT_STRING")!.valueString, RenderHandler.ColorPalette.Cyan);
            DuringTooltip?.Invoke(builder, item);
            builder.AddLine("Effects");
        }
    }
    public static class ModuleEffectHelper
    { public sealed class ModuleEffect { public string displayName = "", description = ""; } }
}

namespace UnityEngine.Localization
{
    public sealed class Locale(string code)
    { public LocaleIdentifier Identifier = new(code); }
    public sealed record LocaleIdentifier(string Code);
}
namespace UnityEngine.Localization.Settings
{
    public static class LocalizationSettings
    { public static UnityEngine.Localization.Locale? SelectedLocale = new("zh"); }
}
namespace Nicokobo.Forge.Registration
{
    public sealed record LocalizedItemText(string Chinese, string English)
    { public string For(bool english) => english ? English : Chinese; }
}
namespace Nicokobo.Forge
{
    internal static class NativeItemRegistry
    {
        internal static readonly HashSet<string> Applied = [];
        internal static bool IsAppliedItem(string id) => Applied.Contains(id);
        internal static bool TryGetAppliedPresentation(string id, out Registration.NativeItemOptions? options,
            out Registration.LocalizedItemText? ownerName)
        { options = null; ownerName = null; return false; }
    }
    internal static class NativeEffectRegistry
    {
        internal static bool TryGetApplied(string id, out ModuleEffectHelper.ModuleEffect? effect)
        { effect = null; return false; }
    }
}
