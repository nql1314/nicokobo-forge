namespace Nicokobo.Forge;

/// <summary>One-session native dialogue visitors. Queue success is not story
/// completion or persistence. After load, content must issue a new request.</summary>
public static class ForgeClientVisitApi
{
    public static bool IsAvailable => ConversationRuntime.IsAvailable;
    public static ForgePresentationResult Queue(string ownerId, string requestId,
        string runId, int slotId, string actorId, string spriteName, ForgeDialogueScript script) =>
        ConversationRuntime.Queue(ownerId, requestId, runId, slotId, actorId, spriteName, script);
    public static ForgePresentationResult Status(string ownerId, string requestId) =>
        ConversationRuntime.Status(ownerId, requestId);
}
