using Data = global::Neemle.OrmLite.Sqlite.SqliteSchemaDataLiterals;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal sealed class SqliteSchemaSession(SqliteProvider provider,
    DbConnection connection, SchemaDiffInspection request, int original,
             SqliteConnection native) : ISchemaDiffApplySession
{
    private DbTransaction? _guarded;
    private bool _disposed;

    internal static async Task<ISchemaDiffApplySession> CreateAsync(SqliteProvider provider,
        DbConnection connection, SchemaDiffInspection request, CancellationToken ct)
    {
        var actual = await SqliteSessionCommands.NativeAsync(connection, ct);
        var original = await ReadAsync(connection, ct);
        return new SqliteSchemaSession(provider, connection, request, original, actual);
    }

    public Task<SchemaDiffObservation> GuardAndInspectAsync(DbTransaction transaction, CancellationToken ct) =>
        SqliteSchemaEntry.RunAsync(() => GuardAsync(transaction, ct), ct);

    private async Task<SchemaDiffObservation> GuardAsync(DbTransaction transaction, CancellationToken ct)
    {
        _guarded = null;
        if (_disposed || transaction.Connection is null) throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        var result = await SqliteSchemaInspect.GuardedAsync(connection, transaction, request, ct);
        _guarded = transaction;
        return result;
    }

    public Task<IReadOnlyList<Statement>> PlanStrictCreateAsync(CreateTable operation,
        DbTransaction transaction, CancellationToken ct) => SqliteSchemaEntry.RunAsync(() =>
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
        SqliteSchemaEntry.RunAsync(() => RestoreAndRetireAsync(ct), ct);

    private async Task<SchemaRestoration> RestoreAndRetireAsync(CancellationToken ct)
    {
        Exception? primary = null;
        try
        {
            var restored = await RestoreAsync(ct);
            if (restored != SchemaRestoration.Unknown) return restored;
        }
        catch (Exception error) { primary = error; }
        var cleanup = new List<Exception>();
        try { SqliteConnection.ClearPool(native); }
        catch (Exception error) { cleanup.Add(error); }
        if (primary is not null || cleanup.Count > 0)
            throw new SchemaDiffException(SchemaDiffError.RestorationUnknown, primary, cleanup);
        return SchemaRestoration.Unknown;
    }

    private async Task<SchemaRestoration> RestoreAsync(CancellationToken ct)
    {
        _guarded = null;
        var current = await ReadAsync(connection, ct);
        if (current != original)
        {
            await SqliteSessionCommands.RunAsync(connection,
                original == 0 ? Data.ForeignKeysOff : Data.ForeignKeysOn, false, ct);
        }
        return await ReadAsync(connection, ct) == original ? SchemaRestoration.Restored : SchemaRestoration.Unknown;
    }

    private static async Task<int> ReadAsync(DbConnection connection, CancellationToken ct)
    {
        var value = await SqliteSessionCommands.RunAsync(connection, Data.ForeignKeysValue, true, ct);
        if (value is not long number || number is not (0 or 1))
            throw new SchemaDiffException(SchemaDiffError.UnknownMetadata);
        return (int)number;
    }

    public ValueTask DisposeAsync() { _disposed = true; _guarded = null; return ValueTask.CompletedTask; }
}
