using System.Data.Common;

namespace Neemle.OrmLite;

internal static class GraphSaveCommands
{
    internal static async Task RunAsync(GraphSaveSession session, GraphSaveStatement step, CancellationToken ct)
    {
        DbCommand? command = null;
        Exception? primary = null;
        try
        {
            command = session.Connection.CreateCommand();
            command.CommandText = step.Statement.Text;
            command.Transaction = session.Transaction;
            foreach (var parameter in step.Statement.Parameters)
                command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
            if (step.Anchor) await AnchorAsync(command, ct);
            else Count(await command.RunGraphSaveNonQueryAsync(ct), step);
        }
        catch (Exception error) { primary = error; throw; }
        finally { if (command is not null) await PolicyCleanup.DisposeAsync(command, primary); }
    }

    private static async Task AnchorAsync(DbCommand command, CancellationToken ct)
    {
        if (await command.RunGraphSaveScalarAsync(ct) is null or DBNull)
            throw GraphSaveRefusal.At(GraphSaveError.MissingAnchor);
    }

    private static void Count(int affected, GraphSaveStatement step)
    {
        if (step.Link && affected is 0 or 1) return;
        if (affected == 1) return;
        throw GraphSaveRefusal.At(affected == 0 && step.Missing is { } missing
            ? missing : GraphSaveError.ProviderFailure);
    }
}
