using System.Text;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;
using L = Neemle.OrmLite.Postgres.PostgresOrphanDataLiterals;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresOrphanHistory(PostgresProvider provider, OrphanGuardSession session,
    PostgresOrphanCatalog catalog)
{
    internal async Task VerifyAsync(PostgresOrphanTable child, OrphanMode mode, CancellationToken ct)
    {
        var triggers = await TriggersAsync(child, ct);
        if (triggers.Count == 0)
        {
            if (session.Shape.Child.Soft is not null) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
            return;
        }
        if (mode == OrphanMode.Delete) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var declared = session.Shape.Child.HistoryTable() ?? throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        if (session.Shape.Child.SoleKey!.Type != ColumnType.Uuid)
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await FreezeFunctionsAsync(ct);
        await RefuseInFlightCatalogWritesAsync(ct);
        await RefuseShadowFunctionsAsync(ct);
        var store = await catalog.ResolveAsync(RowHistoryDdl.TableName, ct);
        await catalog.LockAsync(store, ct);
        await new PostgresOrphanHistoryStore(session, catalog).VerifyAsync(store, ct);
        if (triggers.Count != 2) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        foreach (var trigger in triggers) await VerifyTriggerAsync(trigger, child, declared, ct);
    }

    private async Task<IReadOnlyList<PostgresOrphanTrigger>> TriggersAsync(PostgresOrphanTable table,
        CancellationToken ct)
    {
        var sql = L.HistoryTriggers;
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => new PostgresOrphanTrigger(r.GetString(0), r.GetInt32(1), r.GetFieldValue<byte[]>(2),
                r.GetString(3), r.GetInt64(4)), 256, OrphanError.UnverifiedEffects, ct);
        await InternalTriggersAsync(table, ct);
        await TriggerDependenciesAsync(table, ct);
        return rows;
    }

    internal async Task InternalTriggersAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = L.HistoryInternalTriggers;
        var unknown = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (unknown.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task FreezeFunctionsAsync(CancellationToken ct)
    {
        const string names = L.HistoryFreezeFunctions;
        var sql = L.HistoryFreezeFunctions2 + names + L.HistoryFreezeFunctions3;
        var rights = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetBoolean(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (!rights.Single()) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await session.ExecuteAsync(L.HistoryFreezeFunctions4, ct);
    }

    private async Task VerifyTriggerAsync(PostgresOrphanTrigger trigger, PostgresOrphanTable child,
        RowHistoryTable declared, CancellationToken ct)
    {
        var arguments = new[] { declared.KeyColumn, declared.SoftColumn, declared.TenantColumn }
            .Concat(declared.Unversioned).ToArray();
        if (!trigger.Arguments.SequenceEqual(Encoding.UTF8.GetBytes(string.Join('\0', arguments) + '\0')))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var function = await FunctionAsync(trigger.Function, ct);
        var args = string.Join(L.HistoryTriggerSeparator,
            arguments.Select(a => provider.CreateSerializer().Fragment(new StringLit(a))));
        var prefix = trigger.Events switch
        {
            17 when trigger.Name == RowHistoryDdl.UpdateTrigger => L.HistoryTriggerAfterUpdate + child.Sql
                + L.HistoryTriggerUpdateTail,
            9 when trigger.Name == RowHistoryDdl.DeleteTrigger => L.HistoryTriggerAfterDelete + child.Sql
                + L.HistoryTriggerDeleteTail,
            _ => throw OrphanRefusal.At(OrphanError.UnverifiedEffects),
        };
        var head = L.HistoryCreateTrigger + trigger.Name + prefix;
        var expected = new[] { function, RowHistoryDdl.FunctionName }
            .Select(f => head + f + L.HistoryOpenParen + args + L.HistoryCloseParen);
        if (!expected.Contains(trigger.Definition, StringComparer.Ordinal))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task<string> FunctionAsync(long oid, CancellationToken ct)
    {
        var sql = L.HistoryFunction;
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, oid),
            r => (Name: r.GetString(0), Body: r.GetString(1)), 1, OrphanError.UnverifiedEffects, ct);
        var definition = PostgresLowering.CaptureFunction(OrmLiteSettings.ActorSetting);
        var start = definition.IndexOf("$rh$", StringComparison.Ordinal) + 4;
        var body = definition[start..definition.LastIndexOf("$rh$", StringComparison.Ordinal)];
        if (rows.Count != 1 || rows[0].Body != body) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        return rows[0].Name;
    }

    private async Task RefuseShadowFunctionsAsync(CancellationToken ct)
    {
        var sql = L.HistoryRefuseShadowFunctions;
        var shadowed = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (shadowed.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task TriggerDependenciesAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = L.HistoryTriggerDependencies;
        var unknown = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (unknown.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task RefuseInFlightCatalogWritesAsync(CancellationToken ct)
    {
        await session.ExecuteAsync(L.HistoryRefuseInFlightCatalogWrites, ct);
        var sql = L.HistoryRefuseInFlightCatalogWrites2;
        var active = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (active.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var prepared = await session.ReadAsync(new(L.HistoryRefuseInFlightCatalogWrites3, [], StatementKind.Read),
            r => r.GetInt32(0), 1, OrphanError.UnverifiedEffects, ct);
        if (prepared.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }
}
