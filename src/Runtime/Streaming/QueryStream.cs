namespace Neemle.OrmLite;

public sealed class QueryStream<T> : IAsyncEnumerable<T>
{
    private readonly StreamReadSnapshot<T> _snapshot;
    private readonly CancellationToken _terminal;

    internal QueryStream(StreamReadSnapshot<T> snapshot, CancellationToken terminal)
    {
        _snapshot = snapshot;
        _terminal = terminal;
    }

    public QueryStreamEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default) =>
        QueryStreamExecution.Create(_snapshot, _terminal, ct);

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken ct) => GetAsyncEnumerator(ct);
}
