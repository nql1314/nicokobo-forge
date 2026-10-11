using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

public sealed record ForgePhoneContext(long Generation, string RunId, int SlotId);
public sealed record ForgePhoneContact(string Id, long Number,
    Func<ForgePhoneContext, bool> Visible, Func<ForgePhoneContext, string> DisplayName,
    Func<ForgePhoneContext, ForgeDialogueScript> Call);

/// <summary>Native phone-book contacts. Content owns unlock rules and the call
/// response; Forge never pays, queues visitors or saves on behalf of a contact.</summary>
public static class ForgePhoneApi
{
    public static bool IsAvailable => PhoneContactRuntime.IsAvailable;
    public static ForgePresentationResult Register(string ownerId, ForgePhoneContact contact) =>
        PhoneContactRuntime.Register(ownerId, contact);
}

internal static class PhoneContactRuntime
{
    private const string HookId = "nicokobo.forge.phone_contacts";
    private sealed record Contact(string Owner, ForgePhoneContact Definition);
    private sealed class Call(Contact contact, StorePhoneClient client, PlayerStore store,
        long generation, ForgeDialogueScript script)
    {
        internal readonly Contact Contact = contact;
        internal readonly StorePhoneClient Client = client;
        internal readonly IntPtr Store = store.Pointer;
        internal readonly string Run = store.runID;
        internal readonly int Slot = store.saveSlotId;
        internal readonly long Generation = generation;
        internal readonly ForgeDialogueScript Script = script;
        internal readonly ConversationSelection Selection = new();
        internal readonly List<GameItem> Items = [];
        internal readonly List<Dialogue> Dialogues = [];
        internal readonly List<Il2CppSystem.Action> Callbacks = [];
    }
    private static readonly Dictionary<long, Contact> Contacts = new();
    private static readonly HashSet<long> Conflicts = [];
    private static Action<string>? _log;
    private static bool _installed, _loading;
    private static long _generation;
    private static IntPtr _store;
    private static Call? _call;
    private static DateTime _nextUpdate;
    internal static bool IsAvailable => _installed;

    internal static void Install(bool allowed, Action<string> log)
    {
        _log = log;
        if (!allowed) return;
        try
        {
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.AddChoice),
                [typeof(GameItem), typeof(string), typeof(string), typeof(Dialogue), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Func<bool>)], typeof(Dialogue));
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.SetText), [typeof(string), typeof(string)], typeof(Dialogue));
            NativeHookSet.Require(typeof(Dialogue), nameof(Dialogue.SetNextDialogue), [typeof(Dialogue)], typeof(Dialogue));
            _installed = NativeHookSet.Install(HookId,
            [
                new(typeof(StorePhoneClient), nameof(StorePhoneClient.GetCallDialog), [], typeof(Dialogue), typeof(PhoneContactRuntime), nameof(GetCall)),
                new(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName), [], typeof(string), typeof(PhoneContactRuntime), nameof(GetName)),
                new(typeof(StorePhoneClient), nameof(StorePhoneClient.ShownInPhoneBook), [], typeof(bool), typeof(PhoneContactRuntime), nameof(Shown)),
                new(typeof(PhoneBookUIManager), nameof(PhoneBookUIManager.OpenUI), [], typeof(void), typeof(PhoneContactRuntime), nameof(SyncBeforeOpen)),
                new(typeof(PhoneUIManager), nameof(PhoneUIManager.OpenUI), [], typeof(void), typeof(PhoneContactRuntime), nameof(SyncBeforeOpen)),
                new(typeof(PhoneUIManager), nameof(PhoneUIManager.OnPhoneDialogEnd), [typeof(bool)], typeof(void), typeof(PhoneContactRuntime), Postfix: nameof(EndCall)),
                new(typeof(PlayerStore), nameof(PlayerStore.LoadGame), [], typeof(void), typeof(PhoneContactRuntime), nameof(BeforeReset), nameof(AfterReset)),
                new(typeof(PlayerStore), nameof(PlayerStore.StartNewGame), [], typeof(void), typeof(PhoneContactRuntime), nameof(BeforeReset), nameof(AfterReset)),
                new(typeof(MainMenuUIController), nameof(MainMenuUIController.Awake), [], typeof(void), typeof(PhoneContactRuntime), nameof(BeforeReset))
            ], log);
        }
        catch (Exception ex) { _installed = false; Log(ex); }
    }

    internal static ForgePresentationResult Register(string owner, ForgePhoneContact definition)
    {
        if (!_installed) return new(ForgePresentationStatus.Unavailable);
        if (definition == null || !ConversationContract.Owned(owner, definition.Id) ||
            definition.Number is < 100000 or > 9999999999 || definition.Visible == null ||
            definition.DisplayName == null || definition.Call == null)
            return new(ForgePresentationStatus.Invalid);
        if (Contacts.TryGetValue(definition.Number, out var old))
            return old.Owner == owner && old.Definition.Id == definition.Id
                ? new(ForgePresentationStatus.Conflict, "Contact is already registered")
                : new(ForgePresentationStatus.Conflict, "Phone number belongs to another contact");
        if (Contacts.Values.Any(x => x.Definition.Id == definition.Id)) return new(ForgePresentationStatus.Conflict);
        Contacts.Add(definition.Number, new(owner, definition));
        return new(ForgePresentationStatus.Queued);
    }

    private static PlayerStore? Store()
    {
        if (!_installed || _loading) return null;
        var store = PlayerStore.instance;
        if (store == null || store.Pointer == IntPtr.Zero || string.IsNullOrWhiteSpace(store.runID) || store.saveSlotId < 0) return null;
        if (_store != IntPtr.Zero && _store != store.Pointer) { _generation++; _call = null; Conflicts.Clear(); }
        _store = store.Pointer;
        return store;
    }
    private static ForgePhoneContext Context(PlayerStore store) => new(_generation, store.runID, store.saveSlotId);
    private static Contact? Owned(StorePhoneClient client)
    {
        var store = Store();
        if (store?.PhoneClientDict == null || client == null) return null;
        foreach (var pair in Contacts)
            if (!Conflicts.Contains(pair.Key) && pair.Value.Definition.Id == client.dialogFuncId &&
                store.PhoneClientDict.ContainsKey(pair.Key) && store.PhoneClientDict[pair.Key]?.Pointer == client.Pointer)
                return pair.Value;
        return null;
    }
    private static bool Visible(Contact contact, PlayerStore store)
    {
        try { return contact.Definition.Visible(Context(store)); }
        catch (Exception ex) { Log(ex); return false; }
    }
    private static void SyncBeforeOpen() => UpdateContacts();
    internal static void Update()
    {
        if (!_installed || DateTime.UtcNow < _nextUpdate) return;
        _nextUpdate = DateTime.UtcNow.AddSeconds(1);
        UpdateContacts();
    }
    private static void UpdateContacts()
    {
        try
        {
            var store = Store();
            var dictionary = store?.PhoneClientDict;
            if (store == null || dictionary == null) return;
            foreach (var pair in Contacts)
            {
                if (Conflicts.Contains(pair.Key)) continue;
                var existing = dictionary.ContainsKey(pair.Key) ? dictionary[pair.Key] : null;
                if (existing != null && existing.dialogFuncId != pair.Value.Definition.Id)
                { Conflicts.Add(pair.Key); Log(new InvalidOperationException("Phone-number conflict: " + pair.Key)); continue; }
                if (!Visible(pair.Value, store))
                {
                    if (existing != null) dictionary.Remove(pair.Key);
                    continue;
                }
                if (existing == null)
                {
                    existing = new StorePhoneClient
                    {
                        phoneClientType = StorePhoneClient.PhoneClientType.None,
                        phoneState = StorePhoneClient.PhoneState.Regular,
                        dialogFuncId = pair.Value.Definition.Id, locID = pair.Value.Definition.Id,
                        displayName = pair.Value.Definition.DisplayName(Context(store)),
                        dialedBefore = true, cooldownDuration = 0, currentCooldown = 0,
                        alias = new Il2CppSystem.Collections.Generic.List<string>()
                    };
                    dictionary.Add(pair.Key, existing);
                }
            }
        }
        catch (Exception ex) { Log(ex); }
    }
    private static bool GetName(StorePhoneClient __instance, ref string __result)
    {
        var contact = Owned(__instance); var store = Store();
        if (contact == null || store == null) return true;
        try { __result = contact.Definition.DisplayName(Context(store)); }
        catch (Exception ex) { Log(ex); __result = __instance.displayName; }
        return false;
    }
    private static bool Shown(StorePhoneClient __instance, ref bool __result)
    {
        var contact = Owned(__instance); var store = Store();
        if (contact == null || store == null) return true;
        __result = Visible(contact, store); return false;
    }
    private static bool GetCall(StorePhoneClient __instance, ref Dialogue __result)
    {
        var contact = Owned(__instance); var store = Store();
        if (contact == null || store == null) return true;
        try
        {
            EndCall();
            if (!Visible(contact, store))
            { __result = new Dialogue().SetText(__instance.displayName, "…"); return false; }
            var script = contact.Definition.Call(Context(store));
            if (script == null || script.OwnerId != contact.Owner) throw new InvalidOperationException("Phone dialogue owner mismatch");
            var call = new Call(contact, __instance, store, _generation, script);
            _call = call;
            __result = Build(call);
        }
        catch (Exception ex) { Log(ex); __result = new Dialogue().SetText(__instance.displayName, "…"); }
        return false;
    }
    private static bool Current(Call call)
    {
        if (_call != call || call.Generation != _generation) return false;
        var store = Store(); var phone = PhoneUIManager.Instance;
        return _call == call && call.Generation == _generation && store != null && store.Pointer == call.Store &&
            store.runID == call.Run && store.saveSlotId == call.Slot && phone != null &&
            phone.currentPhoneClient?.Pointer == call.Client.Pointer && Visible(call.Contact, store);
    }
    private static Dialogue Line(Call call, string text)
    {
        var line = new Dialogue().SetText(call.Script.Definition.Speaker, text);
        call.Dialogues.Add(line); return line;
    }
    private static Dialogue Build(Call call)
    {
        var d = call.Script.Definition;
        var root = Line(call, d.Body);
        foreach (var option in d.Choices)
        {
            var preview = Line(call, option.Preview); var response = Line(call, d.FailureText);
            Choice(call, preview, d.ConfirmLabel, response, () =>
            {
                if (!call.Selection.TryEnter(call.Generation, _generation, Current(call))) return;
                string text;
                try { text = option.Confirm(new(call.Generation, call.Run, call.Slot, call.Contact.Owner,
                    "phone:" + call.Contact.Definition.Id, d.Id, option.Id)); }
                catch (Exception ex) { Log(ex); text = d.FailureText; }
                if (Current(call)) response.SetText(d.Speaker, text ?? d.FailureText);
            });
            Choice(call, response, d.LeaveLabel, null, () => Leave(call));
            Choice(call, preview, d.BackLabel, root, null);
            Choice(call, root, option.Label, preview, null);
        }
        Choice(call, root, d.LaterLabel, null, () => Leave(call));
        return root;
    }
    private static void Choice(Call call, Dialogue parent, string label, Dialogue? next, Action? action)
    {
        var item = DirectoryMaster.Item("handnote", true) ?? throw new InvalidOperationException("Phone choice template unavailable");
        call.Items.Add(item);
        Il2CppSystem.Action? callback = null;
        if (action != null)
        {
            callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new Action(() =>
            { try { if (Current(call)) action(); } catch (Exception ex) { Log(ex); } }));
            if (callback == null) throw new InvalidOperationException("Phone callback conversion failed");
            call.Callbacks.Add(callback);
        }
        call.Dialogues.Add(NativeDialogueChoice.Add(parent, item, label, next, callback));
    }
    private static void Leave(Call call)
    {
        if (!Current(call)) return;
        var dialog = DialoguePhoneUIManager.Instance;
        if (dialog != null) dialog.CurrentClientLeave();
    }
    private static void EndCall()
    {
        var call = _call; _call = null;
        if (call == null) return;
        foreach (var item in call.Items)
            try { if (item != null && item.parentInventory == null) item.Destroy(); }
            catch (Exception ex) { Log(ex); }
        call.Items.Clear(); call.Callbacks.Clear(); call.Dialogues.Clear();
    }
    private static void BeforeReset()
    {
        _generation++; _loading = true; _call = null; _store = IntPtr.Zero; Conflicts.Clear();
    }
    private static void AfterReset(bool __runOriginal) { if (__runOriginal) _loading = false; }
    internal static void Uninstall()
    {
        BeforeReset(); _installed = false; Contacts.Clear(); NativeHookSet.Remove(HookId, _log);
    }
    private static void Log(Exception ex) { try { _log?.Invoke("[WARN] [ForgePhone] " + ex); } catch { } }
}
