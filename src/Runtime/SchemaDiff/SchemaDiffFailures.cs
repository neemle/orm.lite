namespace Neemle.OrmLite;

internal static class SchemaDiffFailures
{
    internal static Exception Wrap(Exception primary, IReadOnlyList<Exception> cleanup,
        CancellationToken ct, SchemaDiffApplyResult? result = null)
    {
        if (primary is SchemaDiffCanceledException canceled)
            return new SchemaDiffCanceledException(canceled.InnerException ?? canceled, canceled.CancellationToken,
                canceled.CleanupErrors.Concat(cleanup).ToArray(), result ?? canceled.ApplyResult);
        if (primary is SchemaDiffException known)
            return new SchemaDiffException(known.Error, known.InnerException ?? known,
                known.CleanupErrors.Concat(cleanup).ToArray(), result ?? known.ApplyResult);
        if (primary is OperationCanceledException && ct.IsCancellationRequested)
            return new SchemaDiffCanceledException(primary, ct, cleanup, result);
        return new SchemaDiffException(SchemaDiffError.ProviderFailure, primary, cleanup, result);
    }
}
