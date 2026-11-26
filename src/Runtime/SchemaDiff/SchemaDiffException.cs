namespace Neemle.OrmLite;

public sealed class SchemaDiffException : Exception
{
    internal SchemaDiffException(SchemaDiffError error, Exception? cause = null,
        IReadOnlyList<Exception>? cleanup = null, SchemaDiffApplyResult? result = null)
        : base("Schema diff " + error, cause)
    {
        Error = error;
        ApplyResult = result;
        CleanupErrors = Array.AsReadOnly((cleanup ?? []).ToArray());
    }

    public SchemaDiffError Error { get; }
    public SchemaDiffApplyResult? ApplyResult { get; }
    public IReadOnlyList<Exception> CleanupErrors { get; }
    public override string ToString() => "Schema diff " + Error + "; cleanupErrors=" + CleanupErrors.Count;
}
