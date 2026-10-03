namespace Nicokobo.Forge;

/// <summary>Captures a window's pointer gestures through their release frame,
/// while preserving gestures that began outside the window.</summary>
public sealed class ForgeWindowInputCapture
{
    private bool _open;
    private bool _mouseHeld;
    private bool _capturedGesture;
    private bool _outsideGesture;
    private int _sampleFrame = -1;
    private int _capturedFrame = -1;
    private int _outsideReleaseFrame = -1;

    public void Open(int frame, bool mouseHeld)
    {
        _open = true;
        _mouseHeld = mouseHeld;
        _capturedGesture = mouseHeld;
        _outsideGesture = false;
        _sampleFrame = _capturedFrame = frame;
        _outsideReleaseFrame = -1;
    }

    public void Close(int frame, bool mouseHeld)
    {
        if (!_open) return;
        _open = false;
        _capturedFrame = frame;
        if (!_outsideGesture) _capturedGesture = mouseHeld;
        else if (!mouseHeld)
        {
            _outsideGesture = false;
            _outsideReleaseFrame = frame;
        }
        _mouseHeld = mouseHeld;
        _sampleFrame = frame;
    }

    public bool BlocksPointer(int frame, bool pointerInside, bool mouseHeld)
    {
        if (_sampleFrame != frame)
        {
            _sampleFrame = frame;
            if (mouseHeld && !_mouseHeld)
            {
                _capturedGesture = _open && pointerInside;
                _outsideGesture = !_capturedGesture;
            }
            else if (!mouseHeld && _mouseHeld)
            {
                if (_capturedGesture) _capturedFrame = frame;
                if (_outsideGesture) _outsideReleaseFrame = frame;
                _capturedGesture = _outsideGesture = false;
            }
            _mouseHeld = mouseHeld;
        }
        // Let a native drag receive the mouse-up it owns, even if it ends over
        // this window. The caller must cancel a native drop over the page.
        if (_outsideGesture || _outsideReleaseFrame == frame) return false;
        return _capturedGesture || _capturedFrame == frame || (_open && pointerInside);
    }

    public void Reset()
    {
        _open = _mouseHeld = _capturedGesture = _outsideGesture = false;
        _sampleFrame = _capturedFrame = _outsideReleaseFrame = -1;
    }
}
