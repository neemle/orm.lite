#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Neemle.Orm.Lite;

public sealed record RowHistoryTable(string Table, string KeyColumn, string SoftColumn, string TenantColumn, string[] Unversioned);

public static class RowHistoryDdl
{
    public const string TableName = "row_history";

    public const string VersionPrefix = "9900_orm_history_reconcile_";

    public const string FunctionName = "row_history_capture";

    public const string UpdateTrigger = "row_history_u";

    public const string DeleteTrigger = "row_history_d";

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    public static readonly string[] Preamble = PreambleFor(OrmLiteSettings.ActorSetting);

    internal static string[] PreambleFor(string actorSetting) =>
    [
        "SET LOCAL lock_timeout = '5s'",
        string.Join(' ',
            "CREATE TABLE IF NOT EXISTS row_history (",
            "id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,",
            "tenant_id uuid NULL,",
            "source_table text NOT NULL,",
            "row_id uuid NOT NULL,",
            "op text NOT NULL CHECK (op IN ('update', 'soft_delete', 'restore')),",
            "old_values jsonb NOT NULL,",
            "actor_user_id uuid NULL,",
            "actor_kind text NOT NULL CHECK (actor_kind IN ('user', 'system', 'unknown')),",
            "xact_id bigint NOT NULL,",
            "changed_at timestamptz NOT NULL DEFAULT now())"),
        GuardedIndex("ix_row_history_source_row", "source_table, row_id, id"),
        GuardedIndex("ix_row_history_tenant_xact", "tenant_id, xact_id"),
        GuardedIndex("ix_row_history_changed_at", "changed_at"),
        FunctionSqlFor(actorSetting),
    ];

    public static string[] Reconcile(IReadOnlyList<RowHistoryTable> tables) =>
        ReconcileFor(OrmLiteSettings.ActorSetting, tables);

    internal static string[] ReconcileFor(string actorSetting, IReadOnlyList<RowHistoryTable> tables) =>
        [.. PreambleFor(actorSetting), .. tables.Select(Attach)];

    public static string SetHash(IReadOnlyList<RowHistoryTable> tables) => StatementsHash(Reconcile(tables));

    public static string StatementsHash(IEnumerable<string> statements) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', statements))))[..16];

    public static string ArgumentsHash(RowHistoryTable table) =>
        StatementsHash([table.KeyColumn, table.SoftColumn, table.TenantColumn, .. table.Unversioned]);

    private static string GuardedIndex(string name, string columns) =>
        "DO $rh$ BEGIN IF to_regclass('" + name + "') IS NULL THEN"
        + " CREATE INDEX " + name + " ON row_history (" + columns + ");"
        + " END IF; END $rh$";

    internal static string FunctionSqlFor(string actorSetting)
    {
        OrmLiteSettings.Validate(actorSetting);
        return FunctionText(actorSetting);
    }

    private static string FunctionText(string actorSetting) => string.Join('\n',
        "CREATE OR REPLACE FUNCTION row_history_capture() RETURNS trigger LANGUAGE plpgsql AS $rh$",
        "DECLARE",
        "  v_old jsonb;",
        "  v_new jsonb;",
        "  v_unversioned text[] := ARRAY[]::text[];",
        "  v_delta jsonb;",
        "  v_op text;",
        "  v_actor text;",
        "  v_actor_kind text;",
        "  v_actor_user uuid;",
        "  v_tenant uuid;",
        "  i int;",
        "BEGIN",
        "  IF TG_OP = 'DELETE' THEN",
        "    DELETE FROM row_history",
        "     WHERE source_table = TG_TABLE_NAME AND row_id = (to_jsonb(OLD) ->> TG_ARGV[0])::uuid;",
        "    RETURN NULL;",
        "  END IF;",
        "  v_old := to_jsonb(OLD);",
        "  v_new := to_jsonb(NEW);",
        "  FOR i IN 3 .. TG_NARGS - 1 LOOP",
        "    v_unversioned := v_unversioned || TG_ARGV[i];",
        "  END LOOP;",
        "  v_delta := (SELECT coalesce(jsonb_object_agg(o.key, o.value), '{}'::jsonb)",
        "                FROM jsonb_each(v_old) AS o(key, value)",
        "               WHERE (v_new -> o.key) IS DISTINCT FROM o.value",
        "                 AND NOT (o.key = ANY (v_unversioned)));",
        "  IF v_delta = '{}'::jsonb THEN",
        "    RETURN NULL;",
        "  END IF;",
        "  v_op := CASE",
        "    WHEN v_old ->> TG_ARGV[1] IS NULL AND v_new ->> TG_ARGV[1] IS NOT NULL THEN 'soft_delete'",
        "    WHEN v_old ->> TG_ARGV[1] IS NOT NULL AND v_new ->> TG_ARGV[1] IS NULL THEN 'restore'",
        "    ELSE 'update' END;",
        "  IF TG_ARGV[2] <> '' THEN",
        "    v_tenant := (v_new ->> TG_ARGV[2])::uuid;",
        "  END IF;",
        "  v_actor := current_setting('" + actorSetting + "', true);",
        "  IF v_actor ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN",
        "    v_actor_kind := 'user';",
        "    v_actor_user := v_actor::uuid;",
        "  ELSIF v_actor = 'system' THEN",
        "    v_actor_kind := 'system';",
        "  ELSE",
        "    v_actor_kind := 'unknown';",
        "  END IF;",
        "  INSERT INTO row_history (tenant_id, source_table, row_id, op, old_values, actor_user_id, actor_kind, xact_id)",
        "  VALUES (v_tenant, TG_TABLE_NAME, (v_old ->> TG_ARGV[0])::uuid, v_op, v_delta, v_actor_user, v_actor_kind,",
        "          pg_current_xact_id()::text::bigint);",
        "  RETURN NULL;",
        "END",
        "$rh$");

    private static string Attach(RowHistoryTable t)
    {
        Check(t);
        var table = "\"" + t.Table + "\"";
        var hash = ArgumentsHash(t);
        var arguments = string.Join(", ", new[] { t.KeyColumn, t.SoftColumn, t.TenantColumn }.Concat(t.Unversioned).Select(a => "'" + a + "'"));
        return "DO $rh$ BEGIN IF to_regclass('" + table + "') IS NOT NULL AND ("
            + Missing(table, UpdateTrigger, hash) + " OR " + Missing(table, DeleteTrigger, hash) + ") THEN"
            + " CREATE OR REPLACE TRIGGER " + UpdateTrigger + " AFTER UPDATE ON " + table
            + " FOR EACH ROW WHEN (OLD IS DISTINCT FROM NEW) EXECUTE FUNCTION " + FunctionName + "(" + arguments + ");"
            + " CREATE OR REPLACE TRIGGER " + DeleteTrigger + " AFTER DELETE ON " + table
            + " FOR EACH ROW EXECUTE FUNCTION " + FunctionName + "(" + arguments + ");"
            + " COMMENT ON TRIGGER " + UpdateTrigger + " ON " + table + " IS '" + hash + "';"
            + " COMMENT ON TRIGGER " + DeleteTrigger + " ON " + table + " IS '" + hash + "';"
            + " END IF; END $rh$";
    }

    private static string Missing(string table, string trigger, string hash) =>
        "NOT EXISTS (SELECT 1 FROM pg_trigger g WHERE g.tgrelid = to_regclass('" + table + "')"
        + " AND g.tgname = '" + trigger + "' AND obj_description(g.oid, 'pg_trigger') = '" + hash + "')";

    private static void Check(RowHistoryTable t)
    {
        foreach (var name in new[] { t.Table, t.KeyColumn, t.SoftColumn }.Concat(t.Unversioned))
            if (!Identifier.IsMatch(name))
                throw new ArgumentException("not an identifier: " + name, nameof(t));
        if (t.TenantColumn.Length > 0 && !Identifier.IsMatch(t.TenantColumn))
            throw new ArgumentException("not an identifier: " + t.TenantColumn, nameof(t));
    }
}
