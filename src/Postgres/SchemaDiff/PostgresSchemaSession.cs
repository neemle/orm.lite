using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresSchemaSession(PostgresProvider provider,
    DbConnection connection, SchemaDiffInspection request) : ISchemaDiffApplySession
{
    private DbTransaction? _guarded;
    private bool _disposed;

    internal static Task<ISchemaDiffApplySession> CreateAsync(PostgresProvider provider,
        DbConnection connection, SchemaDiffInspection request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<ISchemaDiffApplySession>(new PostgresSchemaSession(provider, connection, request));
    }

    public Task<SchemaDiffObservation> GuardAndInspectAsync(DbTransaction transaction, CancellationToken ct) =>
        PostgresSchemaEntry.RunAsync(() => GuardAsync(transaction, ct), ct);

    private async Task<SchemaDiffObservation> GuardAsync(DbTransaction transaction, CancellationToken ct)
    {
        _guarded = null;
        if (_disposed || transaction.Connection is null) throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        var read = new PostgresSchemaRead(connection, transaction, request);
        var tables = request.Target.Scope.OwnedTables.Append(MigrationRunner.HistoryTable)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var table in tables) await LockAsync(read, table, ct);
        var result = await PostgresSchemaInspect.GuardedAsync(connection, transaction, request, ct);
        _guarded = transaction;
        return result;
    }

    private static async Task LockAsync(PostgresSchemaRead read, string table, CancellationToken ct)
    {
        var rows = await read.RowsAsync(Data.RelationKind
            + Data.JoinNamespace
            + Data.ResolveCurrentRelation, PostgresSchemaRead.Parameter("table", table), 1, ct);
        if (rows.Count == 0) return;
        if ((string)rows[0][0]! != "r") throw new SchemaDiffException(SchemaDiffError.UnsupportedDifference);
        var name = new SqlWriter().Ident(read.Request.Target.Scope.Namespace).Dot().Ident(table).ToString();
        await read.RowsAsync(Data.LockTablePrefix + name + Data.ExclusiveLockSuffix, [], 1, ct);
    }

    public Task<IReadOnlyList<Statement>> PlanStrictCreateAsync(CreateTable operation,
        DbTransaction transaction, CancellationToken ct) => PostgresSchemaEntry.RunAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (_disposed || transaction != _guarded) throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
            if (!request.Target.Scope.OwnedTables.Contains(operation.Table, StringComparer.Ordinal)
                && operation != (CreateTable)MigrationRunner.CreateHistoryTable())
                throw new SchemaDiffException(SchemaDiffError.Ownership);
            _ = DdlCompiler.Compile(operation, provider);
            if (provider.Lower(operation) is not CreateTable lowered)
                throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
            var written = ((ISchemaDiffDdlSerializer)provider.CreateSerializer()).SerializeStrictCreate(lowered);
            return Task.FromResult<IReadOnlyList<Statement>>([new(written.Text, written.Parameters.ToArray(),
                StatementKind.Ddl)]);
        }, ct);

    public Task<SchemaRestoration> RestoreAndVerifyAsync(CancellationToken ct) =>
        Task.FromResult(SchemaRestoration.NotRequired);

    public ValueTask DisposeAsync() { _disposed = true; _guarded = null; return ValueTask.CompletedTask; }
}
