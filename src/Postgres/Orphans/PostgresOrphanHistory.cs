using System.Text;
using Neemle.OrmLite.Nodes;

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
        var sql = "SELECT t.tgname,CASE WHEN t.tgenabled='O' AND NOT t.tgdeferrable "
            + "AND NOT t.tginitdeferred AND t.tgconstraint=0 AND pg_catalog.cardinality(t.tgattr::smallint[])=0 "
            + "THEN t.tgtype::integer ELSE -1 END,t.tgargs,pg_catalog.pg_get_triggerdef(t.oid),t.tgfoid::bigint "
            + "FROM pg_catalog.pg_trigger t WHERE t.tgrelid=@owner::pg_catalog.oid AND NOT t.tgisinternal LIMIT 257";
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => new PostgresOrphanTrigger(r.GetString(0), r.GetInt32(1), r.GetFieldValue<byte[]>(2),
                r.GetString(3), r.GetInt64(4)), 256, OrphanError.UnverifiedEffects, ct);
        await InternalTriggersAsync(table, ct);
        await TriggerDependenciesAsync(table, ct);
        return rows;
    }

    internal async Task InternalTriggersAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT 1 FROM pg_catalog.pg_trigger t JOIN pg_catalog.pg_proc p ON p.oid=t.tgfoid "
            + "JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
            + "LEFT JOIN pg_catalog.pg_constraint c ON c.oid=t.tgconstraint "
            + "WHERE t.tgrelid=@owner::pg_catalog.oid AND t.tgisinternal AND NOT "
            + "(t.tgenabled='O' AND c.contype='f' AND c.convalidated "
            + "AND n.nspname='pg_catalog' AND pg_catalog.starts_with(p.proname,'RI_FKey_')) LIMIT 1";
        var unknown = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (unknown.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task FreezeFunctionsAsync(CancellationToken ct)
    {
        const string names = "'pg_catalog.pg_proc','pg_catalog.pg_language','pg_catalog.pg_namespace',"
            + "'pg_catalog.pg_class','pg_catalog.pg_type','pg_catalog.pg_operator','pg_catalog.pg_cast'";
        var sql = "SELECT pg_catalog.bool_and(pg_catalog.has_table_privilege(c,'UPDATE,DELETE,TRUNCATE')) "
            + "AND (pg_catalog.current_setting('is_superuser')='on' "
            + "OR pg_catalog.pg_has_role(current_user,'pg_read_all_stats','USAGE')) "
            + "FROM pg_catalog.unnest(ARRAY[" + names + "]) c";
        var rights = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetBoolean(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (!rights.Single()) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await session.ExecuteAsync("LOCK TABLE pg_catalog.pg_proc,pg_catalog.pg_language,pg_catalog.pg_namespace,"
            + "pg_catalog.pg_class,pg_catalog.pg_type,pg_catalog.pg_operator,pg_catalog.pg_cast IN SHARE MODE", ct);
    }

    private async Task VerifyTriggerAsync(PostgresOrphanTrigger trigger, PostgresOrphanTable child,
        RowHistoryTable declared, CancellationToken ct)
    {
        var arguments = new[] { declared.KeyColumn, declared.SoftColumn, declared.TenantColumn }
            .Concat(declared.Unversioned).ToArray();
        if (!trigger.Arguments.SequenceEqual(Encoding.UTF8.GetBytes(string.Join('\0', arguments) + '\0')))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var function = await FunctionAsync(trigger.Function, ct);
        var args = string.Join(", ", arguments.Select(a => provider.CreateSerializer().Fragment(new StringLit(a))));
        var prefix = trigger.Events switch
        {
            17 when trigger.Name == RowHistoryDdl.UpdateTrigger => " AFTER UPDATE ON " + child.Sql
                + " FOR EACH ROW WHEN ((old.* IS DISTINCT FROM new.*)) EXECUTE FUNCTION ",
            9 when trigger.Name == RowHistoryDdl.DeleteTrigger => " AFTER DELETE ON " + child.Sql
                + " FOR EACH ROW EXECUTE FUNCTION ",
            _ => throw OrphanRefusal.At(OrphanError.UnverifiedEffects),
        };
        var head = "CREATE TRIGGER " + trigger.Name + prefix;
        var expected = new[] { function, RowHistoryDdl.FunctionName }.Select(f => head + f + "(" + args + ")");
        if (!expected.Contains(trigger.Definition, StringComparer.Ordinal))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task<string> FunctionAsync(long oid, CancellationToken ct)
    {
        var sql = "SELECT pg_catalog.format('%I.%I',n.nspname,p.proname),p.prosrc "
            + "FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
            + "JOIN pg_catalog.pg_language l ON l.oid=p.prolang WHERE p.oid=@owner::pg_catalog.oid "
            + "AND p.proname='row_history_capture' AND p.prorettype='pg_catalog.trigger'::pg_catalog.regtype "
            + "AND p.pronargs=0 AND NOT p.prosecdef AND p.proconfig IS NULL AND l.lanname='plpgsql' "
            + "FOR SHARE OF p";
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, oid),
            r => (Name: r.GetString(0), Body: r.GetString(1)), 1, OrphanError.UnverifiedEffects, ct);
        var definition = RowHistoryDdl.FunctionSqlFor(provider, OrmLiteSettings.ActorSetting);
        var start = definition.IndexOf("$rh$", StringComparison.Ordinal) + 4;
        var body = definition[start..definition.LastIndexOf("$rh$", StringComparison.Ordinal)];
        if (rows.Count != 1 || rows[0].Body != body) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        return rows[0].Name;
    }

    private async Task RefuseShadowFunctionsAsync(CancellationToken ct)
    {
        var sql = "SELECT 1 WHERE (pg_catalog.current_schemas(true))[1]<>'pg_catalog' OR EXISTS "
            + "(SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace "
            + "WHERE n.nspname=ANY(pg_catalog.current_schemas(true)) AND n.nspname<>'pg_catalog' AND p.proname IN "
            + "('to_jsonb','jsonb_each','jsonb_object_agg','current_setting','pg_current_xact_id'))";
        var shadowed = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (shadowed.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task TriggerDependenciesAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT 1 FROM pg_catalog.pg_depend d JOIN pg_catalog.pg_trigger t ON t.oid=d.objid "
            + "WHERE d.classid='pg_catalog.pg_trigger'::pg_catalog.regclass AND t.tgrelid=@owner::pg_catalog.oid "
            + "AND d.refclassid IN ('pg_catalog.pg_proc'::pg_catalog.regclass,"
            + "'pg_catalog.pg_operator'::pg_catalog.regclass) "
            + "AND d.refobjid>=16384 AND d.refobjid<>t.tgfoid LIMIT 1";
        var unknown = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (unknown.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task RefuseInFlightCatalogWritesAsync(CancellationToken ct)
    {
        await session.ExecuteAsync("SELECT pg_catalog.pg_stat_clear_snapshot()", ct);
        var sql = "SELECT 1 FROM pg_catalog.pg_stat_activity "
            + "WHERE pid<>pg_catalog.pg_backend_pid() AND backend_xid IS NOT NULL LIMIT 1";
        var active = await session.ReadAsync(new(sql, [], StatementKind.Read), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (active.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var prepared = await session.ReadAsync(new("SELECT 1 FROM pg_catalog.pg_prepared_xacts "
            + "WHERE database=pg_catalog.current_database() LIMIT 1", [], StatementKind.Read),
            r => r.GetInt32(0), 1, OrphanError.UnverifiedEffects, ct);
        if (prepared.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }
}
