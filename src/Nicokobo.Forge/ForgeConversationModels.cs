namespace Nicokobo.Forge;

public enum ForgePresentationStatus
{
    Unavailable, Invalid, Conflict, Indeterminate, Queued, Arrived, Dismissed, Opened, Closed
}

public sealed record ForgePresentationResult(ForgePresentationStatus Status, string? Reason = null);
public sealed record ForgeDialogueContext(long Generation, string RunId, int SlotId,
    string OwnerId, string RequestId, string DialogueId, string ChoiceId);

/// <summary>Confirm is called at most once per visit, on the game thread. Revalidate
/// content state/resources here; return the response to display. No save is implied.</summary>
public sealed record ForgeDialogueChoice(string Id, string Label, string Preview,
    Func<ForgeDialogueContext, string> Confirm);
public sealed record ForgeDialogueDefinition(string Id, string Speaker, string Body,
    IReadOnlyList<ForgeDialogueChoice> Choices, string ConfirmLabel, string BackLabel,
    string LeaveLabel, string LaterLabel, string FailureText);

/// <summary>A freshly quoted purchase. Content owns resources and persistence.</summary>
public sealed record ForgeSupplierOffer(string Id, string Label, string Preview,
    Func<ForgeDialogueContext, string> Purchase, string? ConfirmLabel = null);
public sealed record ForgeSupplierPage(string Body, IReadOnlyList<ForgeSupplierOffer> Offers);
public sealed record ForgeSupplierDefinition(string Id, string Speaker,
    Func<ForgeDialogueContext, ForgeSupplierPage> Capture,
    Action<ForgeDialogueContext> Dismiss, string ConfirmLabel, string BackLabel,
    string ContinueLabel, string LeaveLabel, string FailureText);

internal static class SupplierContract
{
    internal static void Validate(string owner, ForgeSupplierDefinition definition)
    {
        if (definition == null || !ConversationContract.Owned(owner, definition.Id) ||
            string.IsNullOrWhiteSpace(definition.Speaker) || definition.Capture == null || definition.Dismiss == null ||
            new[] { definition.ConfirmLabel, definition.BackLabel, definition.ContinueLabel,
                definition.LeaveLabel, definition.FailureText }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid owned supplier definition");
    }
    internal static ForgeSupplierPage FreezePage(ForgeSupplierPage page)
    {
        if (page == null || page.Body == null || page.Offers == null) throw new ArgumentException("Invalid supplier quote page");
        var offers = page.Offers.ToArray();
        if (offers.Any(x => x == null || !ConversationContract.Request(x.Id) ||
                string.IsNullOrWhiteSpace(x.Label) || x.Preview == null || x.Purchase == null ||
                x.ConfirmLabel != null && string.IsNullOrWhiteSpace(x.ConfirmLabel)) ||
            offers.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != offers.Length)
            throw new ArgumentException("Invalid supplier quote offers");
        return page with { Offers = Array.AsReadOnly(offers) };
    }
}

/// <summary>A frozen managed definition. Native objects are owned by the queued visit.</summary>
public sealed class ForgeDialogueScript
{
    internal ForgeDialogueScript(string ownerId, ForgeDialogueDefinition definition)
    { OwnerId = ownerId; Definition = definition; }
    public string OwnerId { get; }
    public ForgeDialogueDefinition Definition { get; }
}

internal static class ConversationContract
{
    internal static bool ValidOwner(string owner) => !string.IsNullOrWhiteSpace(owner) &&
        owner.Split('.').Length >= 2 && owner.Split('.').All(part => part.Length > 0 &&
            part.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'));
    internal static bool Owned(string owner, string id) => ValidOwner(owner) &&
        !string.IsNullOrWhiteSpace(id) && id.StartsWith(owner + ".", StringComparison.Ordinal) && ValidOwner(id);
    internal static bool Request(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 256;
    internal static bool MayOpenReader(string owner, string id, string? heldOwner, string? heldId,
        bool visible, bool sameNativeItem) => !visible || sameNativeItem && owner == heldOwner && id == heldId;

    internal static ForgeDialogueScript Freeze(string owner, ForgeDialogueDefinition definition)
    {
        if (definition == null || !Owned(owner, definition.Id) || string.IsNullOrWhiteSpace(definition.Speaker) ||
            definition.Body == null || definition.Choices == null || definition.Choices.Count == 0 ||
            new[] { definition.ConfirmLabel, definition.BackLabel, definition.LeaveLabel,
                definition.LaterLabel, definition.FailureText }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid owned dialogue definition");
        var choices = definition.Choices.ToArray();
        if (choices.Any(c => c == null || !Request(c.Id) || string.IsNullOrWhiteSpace(c.Label) ||
                c.Preview == null || c.Confirm == null) || choices.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != choices.Length)
            throw new ArgumentException("Dialogue choice IDs must be unique with valid callbacks");
        return new(owner, definition with { Choices = Array.AsReadOnly(choices) });
    }
}

// Used by the native request ledger: no native objects or content save state.
internal sealed class ConversationRequests
{
    internal sealed record Entry(string Owner, string Request, string Actor, string Dialogue,
        ForgePresentationStatus Status);
    private readonly Dictionary<(string Owner, string Request), Entry> _entries = new();
    internal Entry? Find(string owner, string request) => _entries.GetValueOrDefault((owner, request));
    internal ForgePresentationStatus Reserve(string owner, string request, string actor, string dialogue)
    {
        if (Find(owner, request) is { } previous)
            return previous.Actor == actor && previous.Dialogue == dialogue ? previous.Status : ForgePresentationStatus.Conflict;
        if (_entries.Values.Any(x => x.Owner == owner && x.Actor == actor &&
            x.Status is ForgePresentationStatus.Queued or ForgePresentationStatus.Arrived or ForgePresentationStatus.Indeterminate))
            return ForgePresentationStatus.Conflict;
        _entries.Add((owner, request), new(owner, request, actor, dialogue, ForgePresentationStatus.Queued));
        return ForgePresentationStatus.Queued;
    }
    internal void Set(string owner, string request, ForgePresentationStatus status)
    {
        if (Find(owner, request) is { } entry) _entries[(owner, request)] = entry with { Status = status };
    }
    internal void Clear() => _entries.Clear();
}

internal sealed class ConversationSelection
{
    private bool _used;
    internal bool TryEnter(long captured, long current, bool currentVisitor)
    {
        if (_used || captured != current || !currentVisitor) return false;
        _used = true; return true;
    }
}
