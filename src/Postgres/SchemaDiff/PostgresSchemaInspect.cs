using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaInspect
{
    internal static async Task<SchemaDiffObservation> InspectAsync(DbConnection connection,
        SchemaDiffInspection request, CancellationToken ct)
    {
        var tx = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var resources = new PostgresMetadataResources();
        resources.Add(tx);
        SchemaDiffObservation? observation = null;
        Exception? primary = null;
        try
        {
            var read = new PostgresSchemaRead(connection, tx, request);
            await read.RowsAsync(Data.ReadOnlyTransaction, [], 1, ct);
            observation = await CoreAsync(read, ct);
        }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return observation!;
    }

    internal static Task<SchemaDiffObservation> GuardedAsync(DbConnection connection,
        DbTransaction transaction, SchemaDiffInspection request, CancellationToken ct) =>
        CoreAsync(new PostgresSchemaRead(connection, transaction, request), ct);

    private static async Task<SchemaDiffObservation> CoreAsync(PostgresSchemaRead read, CancellationToken ct)
    {
        var connection = read.Connection;
        var request = read.Request;
        var identity = await read.RowsAsync(Data.DatabaseIdentityHead
            + Data.DatabaseVersion
            + Data.DatabaseOid,
            [], 1, ct);
        if (identity.Count == 1 && (!int.TryParse((string)identity[0][2]!, out var version)
            || version is < 190000 or >= 200000)) throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        if (identity.Count != 1 || (string)identity[0][1]! != request.Target.Scope.Namespace)
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
        var tables = new List<SchemaObservedTable>();
        foreach (var table in request.Target.Scope.OwnedTables)
            tables.Add(await PostgresSchemaTable.ReadAsync(read, table, ct));
        var history = await PostgresSchemaHistory.ReadAsync(read, ct);
        var database = PostgresSchemaRead.Fingerprint(new object?[] { connection.DataSource }.Concat(identity[0]));
        return SchemaDiffObservation.FromFacts(new(database, "postgres:schema-diff-v1", true),
            tables, history, request);
    }

}
