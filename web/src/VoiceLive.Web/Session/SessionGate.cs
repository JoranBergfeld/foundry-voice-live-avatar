namespace VoiceLive.Web.Session;

public sealed class SessionGate(int max)
{
    private readonly SemaphoreSlim _slots = new(max, max);
    public int Max { get; } = max;
    public int Active => Max - _slots.CurrentCount;
    public bool TryEnter() => _slots.Wait(0);
    public void Exit() => _slots.Release();
}
