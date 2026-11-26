namespace PolicySmoke;

internal static class Check
{
    private static int _count;
    internal static int Count => _count;

    internal static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
        Interlocked.Increment(ref _count);
    }

    internal static async Task<T> ThrowsAsync<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); }
        catch (T error) { Interlocked.Increment(ref _count); return error; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
