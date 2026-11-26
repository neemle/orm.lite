using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class GraphSaveAcquire(DbConnection connection, Action unregister)
{
    internal ProviderTransaction? Started { get; set; }
    internal TransactionTrace? Trace { get; set; }
    internal UnitOfWork? Unit { get; set; }

    internal async Task FailedAsync(Exception primary)
    {
        if (Unit is not null)
        {
            try { await connection.CloseAsync(); }
            catch (Exception secondary) { PolicyCleanup.Retain(primary, secondary); }
            await PolicyCleanup.DisposeAsync(Unit, primary);
            return;
        }
        var cleanup = new PolicyCleanupSteps(primary);
        cleanup.Run(() => (Trace ?? GatedDataSource.LeaseOf(connection)?.Transaction)?.Dispose());
        cleanup.Run(unregister);
        if (Started is { } started)
            await cleanup.RunAsync(() => started.Transaction.DisposeAsync().AsTask());
        await cleanup.RunAsync(connection.CloseAsync);
        await cleanup.RunAsync(() => connection.DisposeAsync().AsTask());
        if (Started?.Release is { } release) await cleanup.RunAsync(() => release.DisposeAsync().AsTask());
    }
}
