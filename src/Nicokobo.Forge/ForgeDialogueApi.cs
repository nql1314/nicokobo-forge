namespace Nicokobo.Forge;

/// <summary>Managed branching-dialogue definitions. Native controls and delegates
/// are created and retained by ForgeClientVisitApi; content owns all consequences.</summary>
public static class ForgeDialogueApi
{
    public static bool IsAvailable => ConversationRuntime.IsAvailable;
    public static ForgeDialogueScript Create(string ownerId, ForgeDialogueDefinition definition) =>
        ConversationContract.Freeze(ownerId, definition);
}
