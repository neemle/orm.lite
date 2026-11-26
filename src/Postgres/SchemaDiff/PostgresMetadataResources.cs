using System.Runtime.ExceptionServices;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresMetadataResources
{
    private readonly List<IAsyncDisposable> _resources = [];

    internal void Add(IAsyncDisposable resource) => _resources.Insert(0, resource);

    internal async Task FinishAsync(Exception? primary, CancellationToken ct)
    {
        var errors = new List<Exception>();
        foreach (var resource in _resources)
        {
            try { await resource.DisposeAsync(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 0)
        {
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
            return;
        }
        throw Failure(primary, errors, ct);
    }

    private static Exception Failure(Exception? primary, IReadOnlyList<Exception> errors, CancellationToken ct)
    {
        if (primary is SchemaDiffCanceledException canceled)
            return new SchemaDiffCanceledException(canceled.InnerException ?? canceled, canceled.CancellationToken,
                canceled.CleanupErrors.Concat(errors).ToArray(), canceled.ApplyResult);
        if (primary is SchemaDiffException known)
            return new SchemaDiffException(known.Error, known.InnerException ?? known,
                known.CleanupErrors.Concat(errors).ToArray(), known.ApplyResult);
        if (primary is OperationCanceledException && ct.IsCancellationRequested)
            return new SchemaDiffCanceledException(primary, ct, errors);
        var error = primary is null ? SchemaDiffError.CleanupFailure : SchemaDiffError.ProviderFailure;
        return new SchemaDiffException(error,
            primary, errors);
    }
}
