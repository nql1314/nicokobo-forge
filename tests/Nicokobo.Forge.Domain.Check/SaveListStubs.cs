namespace Il2Cpp
{
    public sealed class MainMenuUIController : UnityEngine.Object
    {
        public SaveUIManager saveUIManager = new();
        public ConfirmPanelUIManager confirmPanelUIManager = new();
        public void OnLoadGameClick() { }
    }
    public sealed class SaveUIManager : UnityEngine.Object
    {
        public bool Visible = true, ThrowOnClose;
        public int CloseCalls;
        public void CloseUI()
        {
            CloseCalls++;
            if (ThrowOnClose) throw new InvalidOperationException("Close failed");
            Visible = false;
        }
    }
    public sealed class ConfirmPanelUIManager : UnityEngine.Object
    {
        public string? Message;
        public bool ThrowOnOpen;
        public void OpenPanel(string body, Il2CppSystem.Action? yes, Il2CppSystem.Action? no, bool hideNo)
        {
            if (ThrowOnOpen) throw new InvalidOperationException("Notice failed");
            if (yes != null || no != null || !hideNo) throw new Exception("Unexpected confirmation action");
            Message = body;
        }
    }
}
namespace Nicokobo.Forge
{
    internal static class ForgePresentationApi
    {
        internal static bool PreferEnglish() => false;
    }
}
