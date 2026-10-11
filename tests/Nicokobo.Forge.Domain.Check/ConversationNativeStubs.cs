namespace Il2Cpp;

// Offline port of the native Dialogue contract, not a game/UI test. In the local
// ISIL dumps, both shop and phone DisplayChoice read Item2.plainText; SelectChoice
// runs Item3, assigns Item2, then ContinueConversation advances to nextDialogue.
public sealed class Dialogue
{
    public string title = "", plainText = "";
    public Dialogue? nextDialogue;
    public readonly List<(GameItem Item, Dialogue? Node, Il2CppSystem.Action? Action)> choices = [];
    public Dialogue SetText(string speaker, string text) { title = speaker; plainText = text; return this; }
    public Dialogue SetNextDialogue(Dialogue? next) { nextDialogue = next; return this; }
    public Dialogue AddChoice(GameItem item, string name, string description, Dialogue? node, Il2CppSystem.Action? action)
    { choices.Add((item, node, action)); return this; }
}
