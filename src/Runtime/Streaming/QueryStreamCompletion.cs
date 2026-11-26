namespace Neemle.OrmLite;

public sealed class QueryStreamCompletion
{
    internal QueryStreamCompletion(QueryStreamStopReason reason, long rows, Exception? primary,
        IReadOnlyList<Exception> cleanup)
    {
        Reason = reason;
        RowsYielded = rows;
        PrimaryError = primary;
        CleanupErrors = Array.AsReadOnly(cleanup.ToArray());
    }

    public QueryStreamStopReason Reason { get; }
    public long RowsYielded { get; }
    public bool CleanupSucceeded => CleanupErrors.Count == 0;
    public Exception? PrimaryError { get; }
    public IReadOnlyList<Exception> CleanupErrors { get; }
    public override string ToString() => "Query stream " + Reason + "; rows=" + RowsYielded
        + "; cleanupErrors=" + CleanupErrors.Count;
}
