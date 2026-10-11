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
        Expect(script.Definition.RequireConfirmation, "existing dialogue definitions lost confirmation by default");
        Expect(!ConversationContract.Freeze("test.one", definition with { RequireConfirmation = false })
            .Definition.RequireConfirmation, "freezing a direct-choice dialogue re-enabled confirmation");
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
        Expect(supplier.RequireConfirmation && !InvalidSupplier(supplier with { RequireConfirmation = false }),
            "supplier confirmation default or direct-choice definition rejected");
        Expect(!supplier.AllowMenuRefresh && supplier.CanContinue == null && !InvalidSupplier(supplier with { AllowMenuRefresh = true }),
            "external item refresh must be opt-in and accept the existing supplier constructor");
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
        Expect(page.AutomaticOfferId == null, "ordinary suppliers unexpectedly select purchases themselves");
        var automatic = SupplierContract.FreezePage(page with { AutomaticOfferId = "buy", AutomaticDelaySeconds = 0.6 });
        Expect(automatic.AutomaticOfferId == "buy" && automatic.AutomaticDelaySeconds == 0.6,
            "freezing a page lost its explicit automatic choice");
        Expect(InvalidPage(page with { AutomaticOfferId = "missing" }), "automatic choice targets an absent offer");
        foreach (double delay in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Expect(InvalidPage(automatic with { AutomaticDelaySeconds = delay }), "invalid automatic delay accepted");
        CheckAutomaticSelection(Expect);
        ConversationRuntime.CheckAutomaticOfferAdapter(Expect);
        var firstMenu = new ConversationSelection(); var continueGate = new ConversationSelection();
        var secondMenu = new ConversationSelection();
        Expect(firstMenu.TryEnter(3, 3, true) && continueGate.TryEnter(3, 3, true) && secondMenu.TryEnter(3, 3, true),
            "distinct purchases could not follow one continuation");
        Expect(!firstMenu.TryEnter(3, 3, true) && !continueGate.TryEnter(3, 3, true) && !secondMenu.TryEnter(3, 3, true),
            "stale purchase or duplicate continuation regenerated a gate");
        CheckNativeNavigation(Expect);
        CheckExternalItemRefresh(Expect);
        CheckPagedChoices(Expect);
        Console.WriteLine($"Conversation contracts: {checks} checks passed.");
    }

    private static void CheckAutomaticSelection(Action<bool, string> expect)
    {
        var now = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
        var automatic = new ConversationAutomaticSelection(0.6);
        expect(!automatic.TryEnter(true, now.AddHours(1)), "queuing a page started automatic execution before DisplayChoice");
        automatic.Displayed(now);
        expect(automatic.Pending && !automatic.TryEnter(true, now.AddMilliseconds(599)), "automatic choice skipped its visible pause");
        automatic.Displayed(now.AddMilliseconds(500));
        expect(automatic.TryEnter(true, now.AddMilliseconds(600)), "redisplaying choices postponed the character's answer");
        expect(!automatic.Pending && !automatic.TryEnter(true, now.AddSeconds(2)), "automatic selection repeated or kept holding native input");

        var interrupted = new ConversationAutomaticSelection(0.6);
        interrupted.Displayed(now);
        expect(!interrupted.TryEnter(false, now.AddSeconds(1)), "a hidden menu committed its automatic choice");
        expect(!interrupted.TryEnter(true, now.AddSeconds(3)), "returning to the menu committed an expired background timer");
        expect(!interrupted.TryEnter(true, now.AddSeconds(3.5)) && interrupted.TryEnter(true, now.AddSeconds(3.6)),
            "returning to visible choices did not wait a fresh delay");
    }

    private static void CheckPagedChoices(Action<bool, string> expect)
    {
        foreach (int count in new[] { 3, 4, 5, 8 })
        {
            var root = new Il2Cpp.Dialogue().SetText("Visitor", "Body");
            var reply = new Il2Cpp.Dialogue().SetText("Visitor", "Done");
            var gate = new ConversationSelection();
            int commits = 0;
            for (int i = 0; i < count; i++)
                NativeDialogueChoice.Add(root, new(), "Option " + i, reply,
                    new(() => { if (gate.TryEnter(7, 7, true)) commits++; }));
            // Forge expands the native three-bubble list to four choices.
            var bubbles = new int[4];
            bool overflow = false;
            try { for (int i = 0; i < root.choices.Count; i++) _ = bubbles[i]; }
            catch (IndexOutOfRangeException) { overflow = true; }
            expect(overflow == (count > 4), "fixture did not reproduce the four-choice limit");

            var pages = new List<Il2Cpp.Dialogue> { root };
            NativeDialogueChoice.Paginate(root, bubbles.Length, "More", "Previous",
                text => { var page = new Il2Cpp.Dialogue().SetText("Visitor", text); pages.Add(page); return page; },
                (page, label, next) => NativeDialogueChoice.Add(page, new(), label, next, null));
            expect(count > 4 || pages.Count == 1 && root.choices.Count == count,
                "four or fewer choices must stay on one page");
            var reached = new HashSet<Il2Cpp.Dialogue> { root };
            var found = new HashSet<string>();
            var waiting = new Queue<Il2Cpp.Dialogue>(); waiting.Enqueue(root);
            while (waiting.TryDequeue(out var page))
            {
                expect(page.choices.Count <= bubbles.Length && page.plainText == "Body",
                    "paged choices exceed native capacity or lose the dialogue body");
                foreach (var choice in page.choices)
                {
                    string label = choice.Node!.plainText;
                    var next = choice.Node.nextDialogue;
                    if (label is "More" or "Previous")
                    {
                        expect(choice.Action == null && next != null && pages.Contains(next),
                            "page navigation invokes a transaction or loses its target");
                        if (reached.Add(next!)) waiting.Enqueue(next!);
                    }
                    else
                    {
                        found.Add(label);
                        expect(next == reply, "pagination changed an option's response target");
                        choice.Action!.Invoke();
                    }
                }
            }
            expect(found.SetEquals(Enumerable.Range(0, count).Select(i => "Option " + i)) && reached.Count == pages.Count,
                "a later option or page is unreachable through native selection");
            expect(commits == 1, "changing pages bypassed the once-per-visit transaction gate");
            expect(pages.Count == 1 || pages.Skip(1).All(p => p.choices.Any(c => c.Node!.plainText == "Previous")),
                "a later page cannot return to previous choices");
        }
    }

    private static void CheckExternalItemRefresh(Action<bool, string> expect)
    {
        bool repaired = false;
        int captures = 0, commits = 0;
        long generation = 9;
        var definition = new ForgeSupplierDefinition("test.one.external_item", "Visitor", _ =>
        {
            captures++;
            return repaired ? new("Repaired recording", [new("admit", "Discuss recording", "", _ => { commits++; return "Recorded"; })])
                : new("Repair the physical carrier", []);
        }, _ => { }, "Confirm", "Back", "Continue", "Leave", "Failed") { AllowMenuRefresh = true };
        var initial = SupplierContract.FreezePage(definition.Capture(new(9, "run", 1, "test.one", "request", definition.Id, "capture")));
        var root = new Il2Cpp.Dialogue().SetText(definition.Speaker, initial.Body);
        var resume = new Il2Cpp.Dialogue().SetText(definition.Speaker, definition.FailureText);
        var selection = new ConversationSelection();
        NativeDialogueChoice.Add(root, new(), definition.ContinueLabel, resume, new(() =>
        {
            if (!selection.TryEnter(9, generation, true)) return;
            var page = SupplierContract.FreezePage(definition.Capture(new(9, "run", 1, "test.one", "request", definition.Id, "capture")));
            resume.SetText(definition.Speaker, page.Body);
            foreach (var offer in page.Offers) NativeDialogueChoice.Add(resume, new(), offer.Label, null, null);
        }));
        expect(initial.Offers.Count == 0 && captures == 1, "external repair begins without a dialogue transaction");
        repaired = true;
        var option = root.choices[0];
        option.Action!.Invoke();
        expect(option.Node!.nextDialogue == resume && resume.plainText == "Repaired recording" &&
            resume.choices.Count == 1 && captures == 2 && commits == 0,
            "continuing after physical repair must recapture the current page without purchasing");
        option.Action.Invoke();
        expect(captures == 2 && !selection.TryEnter(9, generation, true), "old refresh or purchase actions cannot reuse the previous menu");
        var stale = new ConversationSelection();
        generation++;
        expect(!stale.TryEnter(9, generation, true) && commits == 0, "an external item refresh cannot enter an old run generation");
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

            var directRoot = new Il2Cpp.Dialogue().SetText("Visitor", "Hello");
            var directResponse = new Il2Cpp.Dialogue().SetText("Visitor", "Failed");
            var directSelection = new ConversationSelection();
            int directCash = 1000, directItems = 0, directCommits = 0, directLeaves = 0;
            long directGeneration = 7;
            var directOption = NativeDialogueChoice.Add(directRoot, new(), "Refund", directResponse, new(() =>
            {
                if (!directSelection.TryEnter(7, directGeneration, true)) return;
                directCash -= 100; directItems++; directCommits++;
                directResponse.SetText("Visitor", "Delivered");
            }));
            NativeDialogueChoice.Add(directResponse, new(), "Leave", null, new(() => directLeaves++));
            expect(directOption.title == "PLAYER" && directOption.plainText == "Refund" &&
                Select(directRoot, 0) == directResponse && directResponse.plainText == "Delivered",
                flow + " direct choice skipped its label, callback or reply");
            expect(directCash == 900 && directItems == 1 && directCommits == 1,
                flow + " direct choice did not settle on first selection");
            Select(directRoot, 0);
            NativeDialogueChoice.Add(directRoot, new(), "Other option", directResponse, new(() =>
            { if (directSelection.TryEnter(7, directGeneration, true)) { directCash -= 100; directItems++; directCommits++; } }));
            Select(directRoot, 1);
            expect(directCash == 900 && directItems == 1 && directCommits == 1,
                flow + " repeated or alternate direct choice settled twice");
            var staleDirect = new ConversationSelection();
            NativeDialogueChoice.Add(directRoot, new(), "Old option", directResponse, new(() =>
            { if (staleDirect.TryEnter(7, directGeneration, true)) directCommits++; }));
            directGeneration++;
            Select(directRoot, 2);
            expect(directCommits == 1, flow + " old-generation direct choice reached content");
            expect(Select(directResponse, 0) == null && directLeaves == 1,
                flow + " direct reply could not end the conversation");
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
