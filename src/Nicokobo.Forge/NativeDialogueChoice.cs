using Il2Cpp;

namespace Nicokobo.Forge;

internal static class NativeDialogueChoice
{
    internal static Dialogue Add(Dialogue parent, GameItem item, string label,
        Dialogue? next, Il2CppSystem.Action? action)
    {
        // DisplayChoice reads this node's plainText. SelectChoice makes it current,
        // then ContinueConversation advances to nextDialogue before displaying it.
        // Both the shop and phone therefore need a player node even for "leave".
        var choice = new Dialogue().SetText("PLAYER", label).SetNextDialogue(next);
        parent.AddChoice(item, label, "", choice, action);
        return choice; // The caller retains this native wrapper for the conversation.
    }
}
