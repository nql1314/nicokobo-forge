namespace Nicokobo.Forge.Workshop;

// Keep a modal's entire closing click, including its release, out of the game.
internal sealed class WorkshopInputCapture
{
    private bool _open;
    private int _releaseFrame = -1;

    internal bool BlocksInput { get; private set; }

    internal void Open()
    {
        _open = true;
        BlocksInput = true;
        _releaseFrame = -1;
    }

    internal void Close()
    {
        if (!_open) return;
        _open = false;
        _releaseFrame = -1;
    }

    internal void Update(int frame, bool mouseHeld)
    {
        if (_open || !BlocksInput) return;
        if (mouseHeld) _releaseFrame = -1;
        else if (_releaseFrame < 0) _releaseFrame = frame;
        else if (frame > _releaseFrame) BlocksInput = false;
    }
}
