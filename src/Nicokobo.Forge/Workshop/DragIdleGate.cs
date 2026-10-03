namespace Nicokobo.Forge.Workshop;

// Observe every frame so short drags also postpone the next background scan.
internal sealed class DragIdleGate
{
    private DateTime _readyAt;

    internal bool Ready(bool dragging, DateTime now)
    {
        if (dragging)
        {
            _readyAt = now.AddMilliseconds(ForgeNumbers.Achievements.DragQuietMilliseconds);
            return false;
        }
        return now >= _readyAt;
    }

    internal void Reset() => _readyAt = DateTime.MinValue;
}
