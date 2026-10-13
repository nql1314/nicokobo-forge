using Il2Cpp;

namespace Nicokobo.Forge;

// Run the production automatic-offer adapter against offline native UI ports.
internal static partial class ConversationRuntime
{
    private sealed class Visit
    {
        internal AutomaticOffer? Automatic;
        internal bool Current = true;
        internal readonly HashSet<IntPtr> DisabledChoices = [];
    }
    private static readonly List<Visit> Visits = [];
    private static bool IsCurrent(Visit visit) => visit.Current;
    private static void Log(Exception ex) => throw new Exception("Automatic-offer adapter failed", ex);

    internal static void CheckAutomaticOfferAdapter(Action<bool, string> expect)
    {
        var menu = new Dialogue();
        var choice = new Dialogue();
        int purchases = 0;
        menu.AddChoice(new(), "Escape", "", choice, new(() => purchases++));
        var automatic = new AutomaticOffer(menu, choice, 0.6);
        var visit = new Visit { Automatic = automatic };
        var bubble = new DialogChoiceBubble();
        var dialog = new DialogUIManager { currentDialog = menu, dialogChoiceBubbles = [bubble] };
        dialog.BeforeSelect = index => BeforeAutomaticChoiceInput(dialog, index);
        DialogUIManager.Instance = dialog;
        HandnoteUIManager.Instance = new();
        AdvCalendarUIManager.Instance = new();
        Visits.Add(visit);
        try
        {
            AfterChoicesDisplayed(dialog);
            expect(!automatic.Selection.TryEnter(true, DateTime.UtcNow.AddHours(1)),
                "hidden choices started the automatic timer");
            bubble.gameObject.activeInHierarchy = true;
            // The native field remains zero even while DisplayChoice exposes buttons.
            expect(dialog.isConversationActiveFlag == 0 && VisibleAutomaticChoice(dialog, automatic) == 0,
                "visible automatic choices were rejected by the unused native conversation flag");
            AfterChoicesDisplayed(dialog);
            expect(!BeforeAutomaticChoiceInput(dialog, 0), "manual input raced the pending automatic offer");
            expect(automatic.Selection.TryEnter(true, DateTime.UtcNow.AddSeconds(1)),
                "displayed choices did not arm the automatic timer");
            expect(BeforeAutomaticChoiceInput(dialog, 0), "spent automatic selection still held input");

            automatic = new(menu, choice, 0.6);
            visit.Automatic = automatic;
            HandnoteUIManager.Instance.panel.activeInHierarchy = true;
            expect(VisibleAutomaticChoice(dialog, automatic) == -1, "reader overlay allowed automatic submission");
            HandnoteUIManager.Instance.panel.activeInHierarchy = false;
            AdvCalendarUIManager.Instance.UIPanel.activeInHierarchy = true;
            expect(VisibleAutomaticChoice(dialog, automatic) == -1, "calendar overlay allowed automatic submission");
            AdvCalendarUIManager.Instance.UIPanel.activeInHierarchy = false;
            ForgeWorkshopApi.IsVisible = true;
            expect(VisibleAutomaticChoice(dialog, automatic) == -1, "workshop overlay allowed automatic submission");
            ForgeWorkshopApi.IsVisible = false;
            dialog.currentDialog = new();
            expect(VisibleAutomaticChoice(dialog, automatic) == -1 && BeforeAutomaticChoiceInput(dialog, 0),
                "another menu was captured by the automatic offer");
            dialog.currentDialog = menu;
            visit.Current = false;
            expect(BeforeAutomaticChoiceInput(dialog, 0), "expired visit still held native input");
            automatic.Selection.Displayed(DateTime.UtcNow.AddSeconds(-1));
            UpdateAutomaticOffers();
            expect(purchases == 0 && dialog.Selected.Count == 0, "expired visit auto-submitted");
            visit.Current = true;
            automatic.Selection.Displayed(DateTime.UtcNow.AddSeconds(-1));
            UpdateAutomaticOffers();
            expect(purchases == 1 && dialog.Selected.SequenceEqual(new[] { 0 }),
                "visible automatic choice did not reach native selection and purchase");
            UpdateAutomaticOffers();
            expect(purchases == 1 && BeforeAutomaticChoiceInput(dialog, 0),
                "automatic purchase repeated or retained the input lock");
            visit.Automatic = null;
            visit.DisabledChoices.Add(choice.Pointer);
            dialog.SelectChoice(0);
            expect(purchases == 1 && dialog.Selected.Count == 1 && menu.choices.Count == 1,
                "disabled option must remain visible without navigating or purchasing");
            var enabled = new Dialogue();
            menu.AddChoice(new(), "Cash repair", "", enabled, new(() => purchases++));
            dialog.SelectChoice(1);
            expect(purchases == 2 && dialog.Selected.SequenceEqual(new[] { 0, 1 }),
                "disabled option blocked another enabled option");
            visit.Current = false;
            expect(BeforeAutomaticChoiceInput(dialog, 0), "expired visit blocked native choices");
        }
        finally
        {
            Visits.Clear();
            DialogUIManager.Instance = null;
            HandnoteUIManager.Instance = null;
            AdvCalendarUIManager.Instance = null;
            ForgeWorkshopApi.IsVisible = false;
        }
    }
}

internal static class ForgeWorkshopApi
{
    internal static bool IsVisible;
}
