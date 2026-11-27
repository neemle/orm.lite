using System.Data;

namespace Neemle.OrmLite;

internal static class OrphanOpen
{
    internal static async Task RunAsync(OrphanUnitAcquire owner, CancellationToken ct)
    {
        var operation = owner.Operation;
        operation.Validate();
        ct.ThrowIfCancellationRequested();
        if (operation.Source is GatedDataSource gated)
        {
            owner.Connection = await gated.OpenStreamConnectionAsync(ct);
            owner.OwnsConnection = true;
        }
        else await PlainAsync(owner, ct);
        if (UnitOfWork.IsOpenOn(owner.Connection!))
        {
            owner.OwnsConnection = false;
            throw OrphanRefusal.At(OrphanError.UnsupportedTransaction);
        }
        operation.Validate();
        ct.ThrowIfCancellationRequested();
    }

    private static async Task PlainAsync(OrphanUnitAcquire owner, CancellationToken ct)
    {
        var operation = owner.Operation;
        var connection = operation.Source.CreateConnection();
        if (UnitOfWork.IsOpenOn(connection) || connection.State != ConnectionState.Closed)
            throw OrphanRefusal.At(OrphanError.UnsupportedTransaction);
        owner.Connection = connection;
        owner.OwnsConnection = true;
        await connection.OpenAsync(ct);
        await (DbActor.Current is { } actor ? operation.Provider.StampActorAsync(connection, actor, ct)
            : operation.Provider.ClearActorAsync(connection, ct));
    }
}
