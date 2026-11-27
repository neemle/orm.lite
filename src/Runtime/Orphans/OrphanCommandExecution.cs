namespace Neemle.OrmLite;

internal static class OrphanCommandExecution
{
    internal static async Task RunAsync(OrphanUnitAcquire owner, OrphanCommand prepared, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        owner.Active();
        var command = owner.Unit!.Connection.CreateCommand();
        Exception? primary = null;
        try
        {
            command.Transaction = owner.Unit.Transaction;
            command.CommandText = prepared.Statement.Text;
            foreach (var parameter in prepared.Statement.Parameters)
                command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
            ct.ThrowIfCancellationRequested();
            owner.Active();
            owner.Budget.ObserveGuard(owner.Guard!.StatementsUsed);
            owner.Budget.Reserve(1);
            // SEC-113-01: orphan writes report like graph-save statements: fixed text, no parameter values, and no
            // provider exception in the event or the Activity, whatever Diagnostics.LogParameters says.
            var affected = await command.RunGraphSaveNonQueryAsync(ct);
            if (affected != 1)
                throw OrphanRefusal.At(affected == 0 ? prepared.Missing : OrphanError.UnsupportedMapping);
            ct.ThrowIfCancellationRequested();
            owner.Active();
        }
        catch (Exception error) { primary = error; throw; }
        finally { await PolicyCleanup.DisposeAsync(command, primary); }
    }
}
