namespace Nicokobo.Forge;

/// <summary>Repeatable purchases in one native visitor. Each refreshed menu has
/// a new confirmation gate; stale menus and duplicate clicks cannot buy twice.</summary>
public static class ForgeSupplierApi
{
    public static bool IsAvailable => ConversationRuntime.IsAvailable;
    public static ForgePresentationResult Queue(string ownerId, string requestId,
        string runId, int slotId, string actorId, string spriteName,
        ForgeSupplierDefinition definition)
    {
        try { SupplierContract.Validate(ownerId, definition); }
        catch (ArgumentException ex) { return new(ForgePresentationStatus.Invalid, ex.Message); }
        var placeholder = ForgeDialogueApi.Create(ownerId, new(definition.Id, definition.Speaker, "",
            [new("supplier", definition.ContinueLabel, "", _ => definition.FailureText)],
            definition.ConfirmLabel, definition.BackLabel, definition.LeaveLabel,
            definition.LeaveLabel, definition.FailureText));
        return ConversationRuntime.Queue(ownerId, requestId, runId, slotId, actorId,
            spriteName, placeholder, definition);
    }
}
