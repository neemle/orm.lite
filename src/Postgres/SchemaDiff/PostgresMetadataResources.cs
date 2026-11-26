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
        if (primary is OperationCanceledException && ct.IsCancellationRequested)
            throw new SchemaDiffCanceledException(primary, ct, errors);
        throw new SchemaDiffException(primary is SchemaDiffException known ? known.Error
            : primary is null ? SchemaDiffError.CleanupFailure : SchemaDiffError.ProviderFailure, primary, errors);
    }
}
