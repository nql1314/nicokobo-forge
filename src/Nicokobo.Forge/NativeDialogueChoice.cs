using Il2Cpp;

namespace Nicokobo.Forge;

internal static class NativeDialogueChoice
{
    internal static void Paginate(Dialogue root, int capacity, string nextLabel, string previousLabel,
        Func<string, Dialogue> createPage, Action<Dialogue, string, Dialogue> addNavigation)
    {
        if (root.choices.Count <= capacity) return;
        if (capacity < 3) throw new InvalidOperationException("Native dialogue needs at least three choice buttons for pagination");
        var choices = root.choices.ToArray();
        root.choices.Clear();
        var page = root;
        Dialogue? previous = null;
        int index = 0;
        while (index < choices.Length)
        {
            int available = capacity - (previous == null ? 0 : 1);
            bool more = choices.Length - index > available;
            int take = more ? available - 1 : choices.Length - index;
            for (int i = 0; i < take; i++) page.choices.Add(choices[index++]);
            if (previous != null) addNavigation(page, previousLabel, previous);
            if (!more) break;
            var next = createPage(root.plainText);
            addNavigation(page, nextLabel, next);
            previous = page;
            page = next;
        }
    }

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
