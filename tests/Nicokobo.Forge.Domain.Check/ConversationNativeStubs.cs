namespace Il2Cpp;

// Offline port of the native Dialogue contract, not a game/UI test. In the local
// ISIL dumps, both shop and phone DisplayChoice read Item2.plainText; SelectChoice
// runs Item3, assigns Item2, then ContinueConversation advances to nextDialogue.
public sealed class Dialogue
{
    private static long _nextPointer;
    public IntPtr Pointer { get; } = new(++_nextPointer);
    public string title = "", plainText = "";
    public Dialogue? nextDialogue;
    public readonly List<(GameItem Item, Dialogue? Node, Il2CppSystem.Action? Action)> choices = [];
    public Dialogue SetText(string speaker, string text) { title = speaker; plainText = text; return this; }
    public Dialogue SetNextDialogue(Dialogue? next) { nextDialogue = next; return this; }
    public Dialogue AddChoice(GameItem item, string name, string description, Dialogue? node, Il2CppSystem.Action? action)
    { choices.Add((item, node, action)); return this; }
}

public sealed class DialogChoiceBubble
{
    public UnityEngine.GameObject gameObject = new();
}

public sealed class DialogUIManager
{
    public static DialogUIManager? Instance;
    public int isConversationActiveFlag = 0;
    public Dialogue? currentDialog;
    public List<DialogChoiceBubble> dialogChoiceBubbles = [];
    public Func<int, bool> BeforeSelect = _ => true;
    public readonly List<int> Selected = [];
    public void SelectChoice(int index)
    {
        if (!BeforeSelect(index)) return;
        Selected.Add(index);
        currentDialog!.choices[index].Action?.Invoke();
    }
}

public sealed class HandnoteUIManager
{
    public static HandnoteUIManager? Instance;
    public UnityEngine.GameObject panel = new();
}

public sealed class AdvCalendarUIManager
{
    public static AdvCalendarUIManager? Instance;
    public UnityEngine.GameObject UIPanel = new();
}
