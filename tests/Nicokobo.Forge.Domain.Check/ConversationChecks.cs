using Nicokobo.Forge;

internal static class ConversationChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool ok, string why) { checks++; if (!ok) throw new Exception("Conversation: " + why); }
        var choices = new[] { new ForgeDialogueChoice("pay", "Pay", "Costs money", _ => "Done") };
        var definition = new ForgeDialogueDefinition("test.one.dialogue", "Visitor", "Hello", choices,
            "Confirm", "Back", "Leave", "Later", "Failed");
        var script = ConversationContract.Freeze("test.one", definition);
        choices[0] = new("changed", "Changed", "", _ => "Wrong");
        Expect(script.Definition.Choices[0].Id == "pay", "caller mutation changed queued choices");
        bool Invalid(ForgeDialogueDefinition d, string owner = "test.one")
        { try { ConversationContract.Freeze(owner, d); return false; } catch (ArgumentException) { return true; } }
        Expect(Invalid(definition, "test.two"), "another owner claimed a dialogue");
        Expect(Invalid(definition with { Choices = [choices[0], choices[0]] }), "duplicate choice ID accepted");
        Expect(Invalid(definition with { Choices = [] }), "empty visit would trap the customer");
        Expect(Invalid(definition with { Choices = [new("id", "Label", "", null!)] }), "missing confirm callback accepted");
        var requests = new ConversationRequests();
        var queued = ForgePresentationStatus.Queued;
        Expect(requests.Reserve("test.one", "1", "test.one.actor", "test.one.dialogue") == queued, "first enqueue rejected");
        requests.Set("test.one", "1", ForgePresentationStatus.Arrived);
        Expect(requests.Reserve("test.one", "1", "test.one.actor", "test.one.dialogue") == ForgePresentationStatus.Arrived,
            "duplicate request generated another visit");
        Expect(requests.Reserve("test.one", "2", "test.one.actor", "test.one.dialogue") == ForgePresentationStatus.Conflict,
            "same actor queued twice while active");
        Expect(requests.Reserve("test.two", "1", "test.two.actor", "test.two.dialogue") == queued,
            "request IDs were not owner scoped");
        Expect(requests.Reserve("test.one", "1", "test.one.other", "test.one.dialogue") == ForgePresentationStatus.Conflict,
            "request ID reused for a different actor");
        Expect(requests.Reserve("test.one", "1", "test.one.actor", "test.one.other") == ForgePresentationStatus.Conflict,
            "request ID silently replaced its dialogue");
        requests.Set("test.one", "1", ForgePresentationStatus.Indeterminate);
        Expect(requests.Reserve("test.one", "2", "test.one.actor", "test.one.dialogue") == ForgePresentationStatus.Conflict,
            "unknown native enqueue allowed automatic retry");
        requests.Set("test.one", "1", ForgePresentationStatus.Dismissed);
        Expect(requests.Reserve("test.one", "1", "test.one.actor", "test.one.dialogue") == ForgePresentationStatus.Dismissed,
            "completed request was replayed");
        Expect(requests.Reserve("test.one", "2", "test.one.actor", "test.one.dialogue") == queued,
            "explicit new visit rejected after dismissal");
        requests.Clear();
        Expect(requests.Find("test.one", "2") == null && requests.Find("test.two", "1") == null,
            "load retained native request handles");
        var gate = new ConversationSelection();
        Expect(!gate.TryEnter(1, 2, true), "old session callback accepted");
        Expect(!gate.TryEnter(2, 2, false), "callback ran for another customer");
        Expect(gate.TryEnter(2, 2, true), "first current selection rejected");
        Expect(!gate.TryEnter(2, 2, true), "reentrant/double click reran content transaction");
        Expect(ConversationContract.MayOpenReader("test.one", "test.one.note", null, null, false, false),
            "closed reader rejected first owner");
        Expect(!ConversationContract.MayOpenReader("test.two", "test.two.note", "test.one", "test.one.note", true, true),
            "another owner stole visible reader");
        Expect(!ConversationContract.MayOpenReader("test.one", "test.one.note", "test.one", "test.one.note", true, false),
            "same owner overwrote a native/external document");
        Expect(!ConversationContract.MayOpenReader("test.one", "test.one.other", "test.one", "test.one.note", true, true),
            "different document replaced visible reader");
        Expect(ConversationContract.MayOpenReader("test.one", "test.one.note", "test.one", "test.one.note", true, true),
            "same live document could not refresh");
        Console.WriteLine($"Conversation contracts: {checks} checks passed.");
    }
}
