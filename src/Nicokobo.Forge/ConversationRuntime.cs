using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

// Shared native lifetime, not a story engine. No file IO, modData or SaveGame calls.
internal static class ConversationRuntime
{
    private const string HookId = "nicokobo.forge.conversations";
    private static Action<string>? _log;
    private static bool _installed, _loading, _openingReader;
    private static long _generation;
    private static IntPtr _storePointer, _displayedNote;
    private static string? _run;
    private static int _slot;
    private static readonly ConversationRequests Requests = new();
    private static readonly List<Visit> Visits = [];
    private static Reading? _reading;
    private static DateTime _nextUpdate;
    internal static bool IsAvailable => _installed;
    internal static bool ReadingAvailable => _installed;
    private sealed record Reading(string Owner, string Id, GameItem Item);
    private sealed class Visit(string owner, string request, string actor, ForgeDialogueScript script,
        long generation, string run, int slot, ForgeSupplierDefinition? supplier = null)
    {
        internal readonly string Owner = owner, Request = request, Actor = actor, Run = run;
        internal readonly int Slot = slot;
        internal readonly long Generation = generation;
        internal readonly ForgeDialogueScript Script = script;
        internal readonly ForgeSupplierDefinition? Supplier = supplier;
        internal StoreClient? Client;
        internal bool ArrivalRequested, Retired, Observed, DismissRequested;
        internal readonly ConversationSelection Selection = new();
        internal readonly List<GameItem> Items = [];
        internal readonly List<Dialogue> Dialogues = [];
        internal readonly List<Il2CppSystem.Action> Callbacks = [];
    }

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) return;
        try
        {
            // Validate call contracts in addition to the all-or-nothing lifecycle hooks.
            NativeHookSet.Require(typeof(StoreClientManager), nameof(StoreClientManager.AddNextClient), [typeof(StoreClient)], typeof(void));
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.AddChoice),
                [typeof(GameItem), typeof(string), typeof(string), typeof(Dialogue), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Func<bool>)], typeof(Dialogue));
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.SetText), [typeof(string), typeof(string)], typeof(Dialogue));
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.SetNextDialogue), [typeof(Dialogue)], typeof(Dialogue));
            NativeHookSet.Require(typeof(DialogUIManager), nameof(DialogUIManager.CurrentClientLeave), [typeof(float)], typeof(void));
            _installed = NativeHookSet.Install(HookId,
            [
                new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void), typeof(ConversationRuntime), nameof(BeforeReset), nameof(AfterReset)),
                new(typeof(PlayerStore), nameof(PlayerStore.StartNewGame), [], typeof(void), typeof(ConversationRuntime), nameof(BeforeReset), nameof(AfterReset)),
                new(typeof(MainMenuUIController), nameof(MainMenuUIController.Awake), [], typeof(void), typeof(ConversationRuntime), nameof(BeforeReset)),
                new(typeof(HandnoteUIManager), nameof(HandnoteUIManager.OpenPanel), [typeof(GameItem)], typeof(void), typeof(ConversationRuntime), Postfix: nameof(AfterReaderOpen)),
                new(typeof(HandnoteUIManager), nameof(HandnoteUIManager.ClosePanel), [], typeof(void), typeof(ConversationRuntime), Postfix: nameof(AfterReaderClose))
            ], log);
        }
        catch (Exception ex) { _installed = false; Log(ex); }
    }

    private static void BeforeReset()
    {
        // Invalidate callbacks before touching UI or invoking any native code.
        _generation++; _loading = true;
        try
        {
            var dialog = DialogUIManager.Instance;
            if (dialog != null && Visits.Any(v => References(dialog, v))) dialog.InterruptDialog();
            if (_reading != null && _displayedNote == _reading.Item.Pointer)
            {
                var reader = HandnoteUIManager.Instance;
                if (reader != null) reader.ClosePanel();
            }
        }
        catch (Exception ex) { Log(ex); }
        // Load/menu teardown owns native object destruction; never touch old wrappers afterwards.
        Visits.Clear(); Requests.Clear(); _reading = null; _displayedNote = IntPtr.Zero;
        _storePointer = IntPtr.Zero; _run = null;
    }
    private static void AfterReset(bool __runOriginal) { if (__runOriginal) _loading = false; }

    private static PlayerStore? Store()
    {
        if (!_installed || _loading) return null;
        var store = PlayerStore.instance; // never use the constructing Instance getter
        if (store == null || store.Pointer == IntPtr.Zero || string.IsNullOrWhiteSpace(store.runID) || store.saveSlotId < 0) return null;
        if (_storePointer != IntPtr.Zero && (_storePointer != store.Pointer || _run != store.runID || _slot != store.saveSlotId))
        {
            BeforeReset(); _loading = false;
        }
        _storePointer = store.Pointer; _run = store.runID; _slot = store.saveSlotId;
        return store;
    }

    internal static ForgePresentationResult Status(string owner, string request)
    {
        if (!ConversationContract.ValidOwner(owner) || !ConversationContract.Request(request)) return new(ForgePresentationStatus.Invalid);
        try
        {
            if (Store() == null) return new(ForgePresentationStatus.Unavailable);
            RefreshVisits(false);
            return new(Requests.Find(owner, request)?.Status ?? ForgePresentationStatus.Unavailable);
        }
        catch (Exception ex) { Log(ex); return new(ForgePresentationStatus.Indeterminate, ex.Message); }
    }

    internal static ForgePresentationResult Queue(string owner, string request, string run, int slot,
        string actor, string sprite, ForgeDialogueScript script, ForgeSupplierDefinition? supplier = null)
    {
        if (!ConversationContract.Owned(owner, actor) || !ConversationContract.Request(request) ||
            script == null || script.OwnerId != owner || string.IsNullOrWhiteSpace(sprite)) return new(ForgePresentationStatus.Invalid);
        Visit? visit = null;
        bool attempted = false;
        try
        {
            var store = Store();
            if (store == null || store.storeState != PlayerStore.StoreState.OPEN) return new(ForgePresentationStatus.Unavailable);
            if (store.runID != run || store.saveSlotId != slot) return new(ForgePresentationStatus.Conflict);
            RefreshVisits(false);
            var previous = Requests.Find(owner, request);
            var status = Requests.Reserve(owner, request, actor, script.Definition.Id);
            if (previous != null || status != ForgePresentationStatus.Queued) return new(status);
            var manager = store.storeClientManager;
            if (manager?.clientStack == null)
            { Requests.Set(owner, request, ForgePresentationStatus.Unavailable); return new(ForgePresentationStatus.Unavailable); }
            if (manager.clientStack.ToArray().Any(x => x != null && x.identifier == actor) ||
                store.currentClientInstance?.GetClientBlueprint()?.identifier == actor)
            { Requests.Set(owner, request, ForgePresentationStatus.Conflict); return new(ForgePresentationStatus.Conflict); }
            visit = new(owner, request, actor, script, _generation, run, slot, supplier);
            var definition = script.Definition;
            visit.Client = new StoreClient
            {
                identifier = actor, displayName = definition.Speaker, spriteName = sprite,
                clientFaction = StoreClient.FACTION_LOWER, clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = true, arrestable = false, nonShootable = true,
                isRegularSellDisable = true, isBargainAvailable = false, isMultiBuyDisabled = true,
                isNoContributeToEvidence = true
            };
            BuildDialogue(visit);
            Visits.Add(visit); // retain callbacks before a native arrival can run
            attempted = true;
            manager.AddNextClient(visit.Client);
            if (!manager.clientStack.ToArray().Any(x => x != null && x.Pointer == visit.Client.Pointer))
                throw new InvalidOperationException("Queue insertion not observed");
            visit.Observed = true;
            return new(ForgePresentationStatus.Queued);
        }
        catch (Exception ex)
        {
            Log(ex);
            var status = attempted ? ForgePresentationStatus.Indeterminate : ForgePresentationStatus.Unavailable;
            Requests.Set(owner, request, status);
            if (!attempted && visit != null) Release(visit);
            return new(status, ex.Message);
        }
    }

    private static Dialogue Line(Visit v, string text)
    {
        var dialogue = new Dialogue().SetText(v.Script.Definition.Speaker, text);
        v.Dialogues.Add(dialogue); return dialogue;
    }
    private static void BuildDialogue(Visit v)
    {
        if (v.Supplier != null)
        {
            var menu = Line(v, v.Supplier.FailureText);
            menu.isMainDialog = true;
            v.Client!.mainDialogue = menu;
            FillSupplierMenu(v, menu);
            return;
        }
        var d = v.Script.Definition;
        var root = Line(v, d.Body);
        root.isMainDialog = true;
        v.Client!.mainDialogue = root;
        foreach (var option in d.Choices)
        {
            var preview = Line(v, option.Preview);
            var response = Line(v, d.FailureText);
            Choice(v, preview, d.ConfirmLabel, response, () =>
            {
                if (!v.Selection.TryEnter(v.Generation, _generation, IsCurrent(v))) return;
                string result;
                try { result = option.Confirm(new(v.Generation, v.Run, v.Slot, v.Owner, v.Request, d.Id, option.Id)); }
                catch (Exception ex) { Log(ex); result = d.FailureText; }
                if (IsCurrent(v)) response.SetText(d.Speaker, result ?? d.FailureText);
            });
            Choice(v, response, d.LeaveLabel, null, () => Leave(v));
            Choice(v, preview, d.BackLabel, root, null);
            Choice(v, root, option.Label, preview, null);
        }
        Choice(v, root, d.LaterLabel, null, () => Leave(v));
    }

    private static ForgeDialogueContext SupplierContext(Visit v, string choice) =>
        new(v.Generation, v.Run, v.Slot, v.Owner, v.Request, v.Supplier!.Id, choice);

    private static void FillSupplierMenu(Visit v, Dialogue menu)
    {
        var d = v.Supplier!;
        ForgeSupplierPage page;
        try
        {
            page = SupplierContract.FreezePage(d.Capture(SupplierContext(v, "capture")));
        }
        catch (Exception ex) { Log(ex); page = new(d.FailureText, []); }
        menu.SetText(d.Speaker, page.Body);
        // One menu represents one quote opportunity. Continue builds a new menu;
        // it never clears an old gate or gives an old callback another chance.
        var selection = new ConversationSelection();
        foreach (var offer in page.Offers.ToArray())
        {
            var preview = Line(v, offer.Preview);
            var response = Line(v, d.FailureText);
            Choice(v, preview, offer.ConfirmLabel ?? d.ConfirmLabel, response, () =>
            {
                if (!selection.TryEnter(v.Generation, _generation, IsCurrent(v))) return;
                string text;
                try { text = offer.Purchase(SupplierContext(v, offer.Id)); }
                catch (Exception ex) { Log(ex); text = d.FailureText; }
                if (IsCurrent(v)) response.SetText(d.Speaker, text ?? d.FailureText);
            });
            var resume = Line(v, d.FailureText);
            var refresh = new ConversationSelection();
            Choice(v, response, d.ContinueLabel, resume, () =>
            {
                if (!refresh.TryEnter(v.Generation, _generation, IsCurrent(v))) return;
                FillSupplierMenu(v, resume);
            });
            Choice(v, response, d.LeaveLabel, null, () => Leave(v));
            Choice(v, preview, d.BackLabel, menu, null);
            Choice(v, menu, offer.Label, preview, null);
        }
        Choice(v, menu, d.LeaveLabel, null, () => Leave(v));
    }

    private static void Choice(Visit v, Dialogue parent, string text, Dialogue? next, Action? action)
    {
        var item = DirectoryMaster.Item("handnote", true) ?? throw new InvalidOperationException("Choice template unavailable");
        v.Items.Add(item);
        Il2CppSystem.Action? callback = null;
        if (action != null)
        {
            callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
            {
                try { if (IsCurrent(v)) action(); }
                catch (Exception ex) { Log(ex); }
            })) ?? throw new InvalidOperationException("Choice callback unavailable");
            v.Callbacks.Add(callback);
        }
        v.Dialogues.Add(NativeDialogueChoice.Add(parent, item, text, next, callback));
    }

    private static bool IsCurrent(Visit v)
    {
        if (v.Retired || v.Generation != _generation) return false;
        var store = Store();
        return v.Generation == _generation && store != null && store.runID == v.Run && store.saveSlotId == v.Slot &&
            store.currentClientInstance?.GetClientBlueprint()?.Pointer == v.Client?.Pointer;
    }
    private static void Leave(Visit v)
    {
        if (!IsCurrent(v) || v.DismissRequested) return;
        v.DismissRequested = true;
        v.Supplier?.Dismiss(SupplierContext(v, "dismiss"));
        v.Client!.CompleteBusiness();
        var dialog = DialogUIManager.Instance;
        if (dialog != null) dialog.CurrentClientLeave();
    }
    private static bool References(DialogUIManager dialog, Visit v) => v.Dialogues.Any(x =>
        x.Pointer == dialog.currentDialog?.Pointer || x.Pointer == dialog.displayingUncompletedDialog?.Pointer);

    internal static void Update()
    {
        if (!_installed || DateTime.UtcNow < _nextUpdate) return;
        _nextUpdate = DateTime.UtcNow.AddMilliseconds(250);
        try
        {
            if (Store() == null) return;
            RefreshVisits(true);
            var reader = HandnoteUIManager.Instance;
            if (_reading != null && (reader == null || reader.panel == null || !reader.panel.activeInHierarchy || _displayedNote != _reading.Item.Pointer))
                ReleaseReading();
        }
        catch (Exception ex) { Log(ex); }
    }

    private static void RefreshVisits(bool callNext)
    {
        var store = Store();
        var queue = store?.storeClientManager?.clientStack;
        if (store == null || queue == null) return;
        var current = store.currentClientInstance?.GetClientBlueprint();
        var queued = queue.ToArray();
        var dialog = DialogUIManager.Instance;
        foreach (var v in Visits.ToArray())
        {
            if (v.Generation != _generation) continue;
            if (current != null && current.Pointer == v.Client?.Pointer)
            {
                v.Observed = true;
                Requests.Set(v.Owner, v.Request, ForgePresentationStatus.Arrived);
            }
            else if (queued.Any(x => x != null && x.Pointer == v.Client?.Pointer))
            {
                v.Observed = true;
                Requests.Set(v.Owner, v.Request, ForgePresentationStatus.Queued);
            }
            else if (v.Observed)
            {
                v.Retired = true;
                Requests.Set(v.Owner, v.Request, ForgePresentationStatus.Dismissed);
                if (dialog == null || !References(dialog, v)) { Release(v); Visits.Remove(v); }
            }
        }
        if (!callNext || current != null || store.isClientArrived || store.storeState != PlayerStore.StoreState.OPEN ||
            ForgeWorkshopApi.IsVisible || dialog == null || dialog.isConversationActiveFlag != 0) return;
        var calendar = AdvCalendarUIManager.Instance;
        var reader = HandnoteUIManager.Instance;
        if (calendar != null && calendar.UIPanel != null && calendar.UIPanel.activeInHierarchy ||
            reader != null && reader.panel != null && reader.panel.activeInHierarchy) return;
        var waiting = Visits.FirstOrDefault(v => !v.Retired && !v.ArrivalRequested &&
            queued.Any(x => x != null && x.Pointer == v.Client?.Pointer));
        if (waiting == null) return;
        waiting.ArrivalRequested = true;
        store.TryCallNextClient();
    }

    private static void Release(Visit v)
    {
        v.Retired = true;
        foreach (var item in v.Items)
            try { if (item != null && item.parentInventory == null) item.Destroy(); }
            catch (Exception ex) { Log(ex); }
        v.Items.Clear(); v.Callbacks.Clear(); v.Dialogues.Clear();
    }

    internal static ForgePresentationResult OpenReading(string owner, string id, string run, int slot,
        string title, string body, string signature)
    {
        if (!ConversationContract.Owned(owner, id) || title == null || body == null || signature == null) return new(ForgePresentationStatus.Invalid);
        try
        {
            var store = Store();
            var reader = HandnoteUIManager.Instance;
            if (store == null || reader == null || reader.panel == null) return new(ForgePresentationStatus.Unavailable);
            if (store.runID != run || store.saveSlotId != slot) return new(ForgePresentationStatus.Conflict);
            if (!ConversationContract.MayOpenReader(owner, id, _reading?.Owner, _reading?.Id,
                reader.panel.activeInHierarchy, _reading != null && _displayedNote == _reading.Item.Pointer))
                return new(ForgePresentationStatus.Conflict, "Another reader is visible");
            if (_reading != null && (_reading.Owner != owner || _reading.Id != id)) ReleaseReading();
            _reading ??= new(owner, id, DirectoryMaster.Item("handnote", true));
            var note = _reading.Item;
            foreach (var entry in new[] { ("HANDNOTE_TITLE", title), ("HANDNOTE_BODY", body), ("HANDNOTE_SIGN", signature) })
            {
                string value = entry.Item2;
                note.ModifyTag(entry.Item1, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
                    new Action<TagState>(tag => { tag.Enable(); tag.valueString = value; })));
            }
            note.SyncModifiedState();
            _openingReader = true;
            try { reader.OpenPanel(note); }
            finally { _openingReader = false; }
            return reader.panel.activeInHierarchy && _displayedNote == note.Pointer
                ? new(ForgePresentationStatus.Opened) : new(ForgePresentationStatus.Indeterminate);
        }
        catch (Exception ex) { Log(ex); return new(ForgePresentationStatus.Indeterminate, ex.Message); }
    }
    internal static ForgePresentationResult CloseReading(string owner, string id)
    {
        if (!ConversationContract.Owned(owner, id)) return new(ForgePresentationStatus.Invalid);
        try
        {
            if (Store() == null) return new(ForgePresentationStatus.Unavailable);
            if (_reading == null) return new(ForgePresentationStatus.Closed);
            if (_reading.Owner != owner || _reading.Id != id || _displayedNote != _reading.Item.Pointer)
                return new(ForgePresentationStatus.Conflict);
            var reader = HandnoteUIManager.Instance;
            if (reader == null) return new(ForgePresentationStatus.Unavailable);
            reader.ClosePanel();
            return reader.panel != null && reader.panel.activeInHierarchy
                ? new(ForgePresentationStatus.Indeterminate) : new(ForgePresentationStatus.Closed);
        }
        catch (Exception ex) { Log(ex); return new(ForgePresentationStatus.Indeterminate, ex.Message); }
    }
    private static void AfterReaderOpen(GameItem __0, bool __runOriginal)
    {
        if (!__runOriginal) return;
        _displayedNote = __0?.Pointer ?? IntPtr.Zero;
        if (!_openingReader && _reading != null && _displayedNote != _reading.Item.Pointer) ReleaseReading();
    }
    private static void AfterReaderClose(bool __runOriginal)
    {
        if (!__runOriginal) return;
        _displayedNote = IntPtr.Zero;
        ReleaseReading();
    }
    private static void ReleaseReading()
    {
        var old = _reading; _reading = null;
        if (old == null) return;
        try { if (old.Item != null && old.Item.parentInventory == null) old.Item.Destroy(); }
        catch (Exception ex) { Log(ex); }
    }
    internal static void Uninstall()
    {
        BeforeReset(); _installed = false;
        NativeHookSet.Remove(HookId, _log);
    }
    private static void Log(Exception ex) { try { _log?.Invoke("[WARN] [Forge/Conversation] " + ex.Message); } catch { } }
}
