namespace Neemle.OrmLite;

internal sealed class StreamCleanup
{
    private readonly List<Exception> _errors = [];

    internal void Add(Exception error) { lock (_errors) if (_errors.Count < 16) _errors.Add(error); }

    internal void Add(IEnumerable<Exception> errors) { foreach (var error in errors) Add(error); }

    internal IReadOnlyList<Exception> Errors { get { lock (_errors) return _errors.ToArray(); } }

    internal async ValueTask<bool> Attempt(Func<ValueTask> cleanup)
    {
        try { await cleanup(); return true; }
        catch (Exception error) { Add(error); return false; }
    }
}
