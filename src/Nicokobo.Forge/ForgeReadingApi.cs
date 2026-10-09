namespace Nicokobo.Forge;

/// <summary>Owner-scoped native reading window. Does not steal another visible
/// reader, create inventory items, write files or call SaveGame.</summary>
public static class ForgeReadingApi
{
    public static bool IsAvailable => ConversationRuntime.ReadingAvailable;
    public static ForgePresentationResult Open(string ownerId, string documentId,
        string runId, int slotId, string title, string body, string signature) =>
        ConversationRuntime.OpenReading(ownerId, documentId, runId, slotId, title, body, signature);
    public static ForgePresentationResult Close(string ownerId, string documentId) =>
        ConversationRuntime.CloseReading(ownerId, documentId);
}
