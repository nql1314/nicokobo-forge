using Il2Cpp;

namespace Nicokobo.Forge;

internal static partial class ConversationRuntime
{
    private sealed class AutomaticOffer(Dialogue menu, Dialogue choice, double delaySeconds)
    {
        internal readonly Dialogue Menu = menu, Choice = choice;
        internal readonly ConversationAutomaticSelection Selection = new(delaySeconds);
    }

    private static AutomaticOffer? CurrentAutomaticOffer(DialogUIManager dialog)
    {
        foreach (var visit in Visits.ToArray())
            if (visit.Automatic is { } automatic && automatic.Selection.Pending && IsCurrent(visit) &&
                dialog.currentDialog?.Pointer == automatic.Menu.Pointer)
                return automatic;
        return null;
    }

    private static void AfterChoicesDisplayed(DialogUIManager __instance)
    {
        try
        {
            var automatic = CurrentAutomaticOffer(__instance);
            if (automatic != null && VisibleAutomaticChoice(__instance, automatic) >= 0)
                automatic.Selection.Displayed(DateTime.UtcNow);
        }
        catch (Exception ex) { Log(ex); }
    }

    private static bool BeforeAutomaticChoiceInput(DialogUIManager __instance)
    {
        // Only this owned automatic page holds input. The timer spends its gate
        // before invoking native SelectChoice, so the automatic call passes through.
        try { return CurrentAutomaticOffer(__instance) == null; }
        catch (Exception ex) { Log(ex); return true; }
    }

    private static int VisibleAutomaticChoice(DialogUIManager dialog, AutomaticOffer automatic)
    {
        // Native DisplayChoice exposes live buttons without setting isConversationActiveFlag.
        // Match the owned page and its visible button instead of that unused field.
        if (dialog.currentDialog?.Pointer != automatic.Menu.Pointer ||
            dialog.dialogChoiceBubbles == null || ForgeWorkshopApi.IsVisible) return -1;
        var reader = HandnoteUIManager.Instance;
        var calendar = AdvCalendarUIManager.Instance;
        if (reader != null && reader.panel != null && reader.panel.activeInHierarchy ||
            calendar != null && calendar.UIPanel != null && calendar.UIPanel.activeInHierarchy) return -1;
        for (int i = 0; i < automatic.Menu.choices.Count && i < dialog.dialogChoiceBubbles.Count; i++)
            if (automatic.Menu.choices[i].Item2?.Pointer == automatic.Choice.Pointer &&
                dialog.dialogChoiceBubbles[i] != null && dialog.dialogChoiceBubbles[i].gameObject.activeInHierarchy)
                return i;
        return -1;
    }

    private static void UpdateAutomaticOffers()
    {
        var dialog = DialogUIManager.Instance;
        foreach (var visit in Visits.ToArray())
        {
            if (visit.Automatic is not { } automatic || !automatic.Selection.Pending) continue;
            int index = dialog != null && IsCurrent(visit) ? VisibleAutomaticChoice(dialog, automatic) : -1;
            if (!automatic.Selection.TryEnter(index >= 0, DateTime.UtcNow)) continue;
            // Native selection owns navigation and calls the same once-only Purchase
            // callback as a click. Resource checks and persistence stay with content.
            dialog!.SelectChoice(index);
        }
    }
}
