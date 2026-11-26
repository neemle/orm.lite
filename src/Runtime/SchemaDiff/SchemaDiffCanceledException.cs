namespace Neemle.OrmLite;

public sealed class SchemaDiffCanceledException : OperationCanceledException
{
    internal SchemaDiffCanceledException(Exception cause, CancellationToken ct,
        IReadOnlyList<Exception>? cleanup = null, SchemaDiffApplyResult? result = null)
        : base("Schema diff canceled", cause, ct)
    {
        ApplyResult = result;
        CleanupErrors = Array.AsReadOnly((cleanup ?? []).ToArray());
    }

    public SchemaDiffApplyResult? ApplyResult { get; }
    public IReadOnlyList<Exception> CleanupErrors { get; }
    public override string ToString() => "Schema diff canceled; cleanupErrors=" + CleanupErrors.Count;
}
