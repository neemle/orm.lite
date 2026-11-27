namespace Neemle.OrmLite;

internal static class JoinReadFailures
{
    internal static Exception Observed(Exception error, CancellationToken token, StreamCleanup cleanup)
    {
        if (error is StreamOpenFailure open)
        {
            error = open.Primary;
            cleanup.Add(open.Cleanup);
        }
        if (error is JoinReadException or JoinReadCanceledException) return error;
        if (error is OperationCanceledException && token.IsCancellationRequested)
            return new JoinReadCanceledException(token, error);
        var category = error is QueryStreamException ? JoinError.ResourceMismatch : JoinError.ProviderFailure;
        return new JoinReadException(category, error);
    }

    internal static Exception Retain(Exception primary, IReadOnlyList<Exception> cleanup)
    {
        foreach (var error in cleanup) PolicyCleanup.Retain(primary, error);
        return primary;
    }
}
