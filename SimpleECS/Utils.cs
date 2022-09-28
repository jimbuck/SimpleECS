namespace SimpleECS;

/// <summary>
/// Create IDs that can be resused.
/// <para>Based on Doraku/DefaultEcs</para>
/// </summary>
internal sealed class IdPool
{
    private readonly ConcurrentStack<int> _ids = new();
    private int _last = 0;

    public int Last => _last;

    public int Next()
    {
        if (!_ids.TryPop(out int freeInt)) freeInt = Interlocked.Increment(ref _last);

        return freeInt;
    }

    public void Release(int releasedInt) => _ids.Push(releasedInt);
}