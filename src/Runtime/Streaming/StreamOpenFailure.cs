namespace Neemle.OrmLite;

internal sealed class StreamOpenFailure(Exception primary, IReadOnlyList<Exception> cleanup)
    : Exception("Query stream open failed", primary)
{
    internal Exception Primary { get; } = primary;
    internal IReadOnlyList<Exception> Cleanup { get; } = cleanup;
    public override string ToString() => "Query stream open failed; cleanupErrors=" + Cleanup.Count;
}
