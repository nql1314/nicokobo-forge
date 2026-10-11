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
        var supplier = new ForgeSupplierDefinition("test.one.supplier", "Supplier", _ => new("Stock", []),
            _ => { }, "Buy", "Back", "Continue", "Leave", "Failed");
        bool InvalidSupplier(ForgeSupplierDefinition d, string owner = "test.one")
        { try { SupplierContract.Validate(owner, d); return false; } catch (ArgumentException) { return true; } }
        Expect(!InvalidSupplier(supplier), "valid supplier was rejected");
        Expect(InvalidSupplier(supplier, "test.two"), "another owner claimed a supplier");
        Expect(InvalidSupplier(supplier with { Capture = null! }) && InvalidSupplier(supplier with { Dismiss = null! }),
            "supplier omitted stock capture or dismissal");
        var offers = new[] { new ForgeSupplierOffer("buy", "Buy two", "Total 84", _ => "Delivered") };
        var page = SupplierContract.FreezePage(new("Finite stock", offers));
        offers[0] = new("changed", "Changed", "", _ => "Wrong");
        Expect(page.Offers[0].Id == "buy", "caller mutation replaced a quoted purchase");
        bool InvalidPage(ForgeSupplierPage p)
        { try { SupplierContract.FreezePage(p); return false; } catch (ArgumentException) { return true; } }
        Expect(InvalidPage(new("Stock", [offers[0], offers[0]])), "duplicate quote IDs accepted");
        Expect(InvalidPage(new("Stock", [new("buy", "Buy", "", null!)])), "purchase callback absent");
        Expect(SupplierContract.FreezePage(new("Sold out", [])).Offers.Count == 0, "sold-out visitor could not reach dismissal");
        var firstMenu = new ConversationSelection(); var continueGate = new ConversationSelection();
        var secondMenu = new ConversationSelection();
        Expect(firstMenu.TryEnter(3, 3, true) && continueGate.TryEnter(3, 3, true) && secondMenu.TryEnter(3, 3, true),
            "distinct purchases could not follow one continuation");
        Expect(!firstMenu.TryEnter(3, 3, true) && !continueGate.TryEnter(3, 3, true) && !secondMenu.TryEnter(3, 3, true),
            "stale purchase or duplicate continuation regenerated a gate");
        CheckNativeNavigation(Expect);
        Console.WriteLine($"Conversation contracts: {checks} checks passed.");
    }

    private static void CheckNativeNavigation(Action<bool, string> expect)
    {
        Il2Cpp.Dialogue? Select(Il2Cpp.Dialogue menu, int index)
        {
            var selected = menu.choices[index];
            selected.Action?.Invoke();
            // Native SelectChoice -> ContinueConversation, from both local ISIL dumps.
            return selected.Node?.nextDialogue;
        }
        var brokenRoot = new Il2Cpp.Dialogue().SetText("Visitor", "Hello");
        var brokenPreview = new Il2Cpp.Dialogue().SetText("Visitor", "Costs 100");
        brokenRoot.AddChoice(new(), "Refund", "", brokenPreview, null);
        expect(Select(brokenRoot, 0) == null && brokenRoot.choices[0].Node!.plainText != "Refund",
            "fixture must reproduce direct binding skipping preview and displaying the wrong label");

        // Dialogue, core trading, supply and phone all use this production adapter.
        foreach (string flow in new[] { "story", "core", "supplier", "phone" })
        {
            var root = new Il2Cpp.Dialogue().SetText("Visitor", "Hello");
            var preview = new Il2Cpp.Dialogue().SetText("Visitor", "Costs 100; gives one item");
            var response = new Il2Cpp.Dialogue().SetText("Visitor", "Failed");
            var selection = new ConversationSelection();
            int cash = 1000, items = 0, commits = 0, leaves = 0;
            long currentGeneration = 7;
            var option = NativeDialogueChoice.Add(root, new(), "Refund", preview, null);
            NativeDialogueChoice.Add(preview, new(), "Confirm", response, new(() =>
            {
                if (!selection.TryEnter(7, currentGeneration, true)) return;
                cash -= 100; items++; commits++;
                response.SetText("Visitor", "Delivered");
            }));
            NativeDialogueChoice.Add(preview, new(), "Back", root, null);
            var leave = NativeDialogueChoice.Add(response, new(), "Leave", null, new(() => leaves++));
            expect(option.title == "PLAYER" && option.plainText == "Refund", flow + " option label is not player text");
            expect(Select(root, 0) == preview && cash == 1000 && items == 0, flow + " preview was skipped or paid early");
            expect(Select(preview, 1) == root && commits == 0, flow + " back committed a transaction or skipped the menu");
            expect(Select(root, 0) == preview && Select(preview, 0) == response && response.plainText == "Delivered",
                flow + " confirmation failed to show the callback response");
            expect(cash == 900 && items == 1 && commits == 1, flow + " confirmation did not settle once");
            Select(preview, 0);
            expect(cash == 900 && items == 1 && commits == 1, flow + " repeated confirm paid or granted twice");
            expect(leave.plainText == "Leave" && Select(response, 0) == null && leaves == 1,
                flow + " leave has no display node or fails to end the conversation");

            var stale = new ConversationSelection();
            NativeDialogueChoice.Add(root, new(), "Old confirm", response, new(() =>
            { if (stale.TryEnter(7, currentGeneration, true)) commits++; }));
            currentGeneration++;
            Select(root, 1);
            expect(commits == 1, flow + " old-generation confirm reached content");
        }

        var result = new Il2Cpp.Dialogue().SetText("Supplier", "Delivered");
        var resume = new Il2Cpp.Dialogue().SetText("Supplier", "Loading");
        var continuation = new ConversationSelection();
        int captures = 0;
        NativeDialogueChoice.Add(result, new(), "Continue", resume, new(() =>
        {
            if (!continuation.TryEnter(1, 1, true)) return;
            captures++;
            resume.SetText("Supplier", "Updated stock");
        }));
        expect(Select(result, 0) == resume && resume.plainText == "Updated stock" && captures == 1,
            "supplier continuation skipped the refreshed menu");
        Select(result, 0);
        expect(captures == 1, "supplier duplicate continuation refreshed twice");
    }
}
