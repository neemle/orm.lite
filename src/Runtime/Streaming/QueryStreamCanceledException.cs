namespace Neemle.OrmLite;

public sealed class QueryStreamCanceledException : OperationCanceledException
{
    internal QueryStreamCanceledException(long rows, IReadOnlyList<Exception> cleanup,
        Exception cause, CancellationToken token) : base("Query stream canceled; rows=" + rows, cause, token)
    {
        RowsYielded = rows;
        CleanupErrors = Array.AsReadOnly(cleanup.ToArray());
    }

    public long RowsYielded { get; }
    public IReadOnlyList<Exception> CleanupErrors { get; }
    public override string ToString() => "Query stream canceled; rows=" + RowsYielded
        + "; cleanupErrors=" + CleanupErrors.Count;
}
