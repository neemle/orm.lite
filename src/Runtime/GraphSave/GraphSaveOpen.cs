using System.Data.Common;

namespace Neemle.OrmLite;

internal static class GraphSaveOpen
{
    internal static async Task<DbConnection> ConnectionAsync(OrmContext context, CancellationToken ct) =>
        context.DataSource is GatedDataSource gated ? await gated.OpenGraphSaveConnectionAsync(ct)
            : await context.DataSource.OpenConnectionAsync(ct);

    internal static async ValueTask<DbConnection> LeaseAsync(DbConnection connection,
        Func<DbConnection, ValueTask<DbConnection>> lease)
    {
        try { return await lease(connection); }
        catch (Exception primary)
        {
            var cleanup = new StreamCleanup();
            await cleanup.Attempt(() => new ValueTask(connection.CloseAsync()));
            var disposed = await cleanup.Attempt(connection.DisposeAsync);
            if (disposed)
            {
                try { GatedDataSource.LeaseOf(connection)?.ReleaseStream(); }
                catch (Exception error) { cleanup.Add(error); }
            }
            foreach (var error in cleanup.Errors) PolicyCleanup.Retain(primary, error);
            throw;
        }
    }
}
