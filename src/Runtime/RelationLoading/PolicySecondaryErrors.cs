namespace Neemle.OrmLite;

internal sealed class PolicySecondaryErrors
{
    private readonly List<Exception> _errors = [];
    internal void Add(Exception error)
    {
        lock (_errors)
            if (_errors.Count < 4) _errors.Add(error);
    }
}
