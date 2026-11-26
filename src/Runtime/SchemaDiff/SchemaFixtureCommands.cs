using System.Data.Common;

namespace Neemle.OrmLite;

internal static class SchemaFixtureCommands
{
    internal static async Task ExecuteAsync(DbConnection connection, DbTransaction? transaction,
        Statement statement, CancellationToken ct)
    {
        DbCommand? command = null; Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            command = connection.CreateCommand(); command.Transaction = transaction;
             command.CommandText = statement.Text;
            foreach (var parameter in statement.Parameters) command.AddParam(parameter.Name, parameter.Value);
            await command.RunNonQueryAsync(statement.Kind, ct);
        }
        catch (Exception error) { primary = error; }
        await SchemaResourceCleanup.DisposeAsync(command, cleanup);
        if (primary is not null) throw SchemaDiffFailures.Wrap(primary, cleanup, ct);
        if (cleanup.Count > 0) throw new SchemaDiffException(SchemaDiffError.CleanupFailure, cleanup: cleanup);
    }
}
