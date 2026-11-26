namespace Neemle.OrmLite;

public sealed class QueryStreamException : Exception
{
    internal QueryStreamException(QueryStreamError error, long rows = 0,
        IReadOnlyList<Exception>? cleanup = null, Exception? cause = null)
        : base("Query stream " + error + "; rows=" + rows, cause)
    {
        Error = error;
        RowsYielded = rows;
        CleanupErrors = Array.AsReadOnly((cleanup ?? []).ToArray());
    }

    public QueryStreamError Error { get; }
    public long RowsYielded { get; }
    public IReadOnlyList<Exception> CleanupErrors { get; }
    public override string ToString() => "Query stream " + Error + "; rows=" + RowsYielded
        + "; cleanupErrors=" + CleanupErrors.Count;
}
