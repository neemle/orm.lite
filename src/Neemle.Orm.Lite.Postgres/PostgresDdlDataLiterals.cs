namespace Neemle.Orm.Lite.Postgres;

internal static class PostgresDdlDataLiterals
{
    internal const string RowHistoryTableName = "row_history";

    internal const string RowHistoryFunctionName = "row_history_capture";

    internal const string RowHistoryUpdateTrigger = "row_history_u";

    internal const string RowHistoryDeleteTrigger = "row_history_d";

    internal const string LockTimeoutSetting = "lock_timeout";

    internal const string LockTimeoutValue = "5s";

    internal const string ListSeparator = ", ";

    internal const string ExpressionTerm = "<expression>";

    internal const string TempSchema = "pg_temp";

    internal const string DriftCurrent = "current_index";

    internal const string DriftProbeName = "orm_ix_check";

    internal const string IndexOfTable = "pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid";

    internal const string ClassWithNamespace = "pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace";

    internal const string DriftProbeTable = "CREATE TEMP TABLE {0} (LIKE {1})";

    internal const string IndexNameTakenMessage =
        "Index name % is already held by another relation in the schema of table %: PostgreSQL index names are "
        + "schema-global, so creating it would be skipped and the table would stay without its index. "
        + "Rename the index.";

    internal const string DriftDropProbe = "DROP TABLE pg_temp.\"orm_ix_check\"";

    internal const string TotalIndexVariable = "total_index";

    internal const string LiveIndexVariable = "live_index";

    internal const string DropTotalByOid = "EXECUTE 'DROP INDEX ' || total_index::regclass";

    internal const string DropLiveByOid = "EXECUTE 'DROP INDEX ' || live_index::regclass";

    internal const string CommentLiveNullTemplate = "COMMENT ON INDEX %s IS NULL";

    internal const string DriftDropCurrent = "EXECUTE 'DROP INDEX ' || current_index::regclass";

    internal const string DriftTablePattern = " ON \\S+ USING ";

    internal const string DriftTableReplacement = " USING ";

    internal const string DriftOn = " ON ";

    internal const string RowHistoryTableDdl =
        "CREATE TABLE IF NOT EXISTS row_history ( id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, tenant_id uuid NULL, "
        + "source_table text NOT NULL, row_id uuid NOT NULL, op text NOT NULL CHECK (op IN ('update', 'soft_delete', 'restore')), "
        + "old_values jsonb NOT NULL, actor_user_id uuid NULL, actor_kind text NOT NULL CHECK (actor_kind IN ('user', 'system', 'unknown')), "
        + "xact_id bigint NOT NULL, changed_at timestamptz NOT NULL DEFAULT now())";

    internal const string RecreateIndexMessage =
        "cannot recreate unique index {0} on {1} ({2}): duplicate key % exists, nothing was deleted";

    internal const string DropSoftColumnMessage =
        "cannot drop {0} from {1}: % soft-deleted row(s) would read as live";

    internal const string LoweredKeyTerm = "lower({0})";

    internal const string CollisionLoopTemplate =
        "FOR r IN SELECT ic.relname::text AS idx, c.conname::text AS con FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid"
        + " LEFT JOIN pg_constraint c ON c.conindid = i.indexrelid AND c.contype IN ('u', 'p')"
        + " WHERE i.indrelid = to_regclass({0}) AND i.indisunique AND i.indpred IS NULL AND i.indexprs IS NULL"
        + " AND i.indnkeyatts = {1}"
        + " AND (SELECT count(*) FROM unnest(i.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)"
        + " JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum"
        + " WHERE k.ord <= i.indnkeyatts AND a.attname = ANY (ARRAY[{2}])) = {1}"
        + " LOOP RAISE EXCEPTION 'Live-only index % on table % over columns (%) cannot take effect: % % is unique over the same columns without a live-row predicate',"
        + " {3}, {4}, {5},"
        + " CASE WHEN r.con IS NULL THEN 'index' ELSE 'constraint' END, COALESCE(r.con, r.idx)"
        + " USING HINT = 'Convert it with an explicit migration: create the partial index, then drop the total one.';"
        + " END LOOP";

    internal const string ServerVersionQuery = "SELECT current_setting('server_version_num')";

    internal const string DropOwnerConstraintTemplate = "ALTER TABLE {0} DROP CONSTRAINT %I";

    internal const string CaptureFunctionHead =
        "CREATE OR REPLACE FUNCTION row_history_capture() RETURNS trigger LANGUAGE plpgsql AS $rh$" + "\n" +
        "DECLARE" + "\n" +
        "  v_old jsonb;" + "\n" +
        "  v_new jsonb;" + "\n" +
        "  v_unversioned text[] := ARRAY[]::text[];" + "\n" +
        "  v_delta jsonb;" + "\n" +
        "  v_op text;" + "\n" +
        "  v_actor text;" + "\n" +
        "  v_actor_kind text;" + "\n" +
        "  v_actor_user uuid;" + "\n" +
        "  v_tenant uuid;" + "\n" +
        "  i int;" + "\n" +
        "BEGIN" + "\n" +
        "  IF TG_OP = 'DELETE' THEN" + "\n" +
        "    DELETE FROM row_history" + "\n" +
        "     WHERE source_table = TG_TABLE_NAME AND row_id = (to_jsonb(OLD) ->> TG_ARGV[0])::uuid;" + "\n" +
        "    RETURN NULL;" + "\n" +
        "  END IF;" + "\n" +
        "  v_old := to_jsonb(OLD);" + "\n" +
        "  v_new := to_jsonb(NEW);" + "\n" +
        "  FOR i IN 3 .. TG_NARGS - 1 LOOP" + "\n" +
        "    v_unversioned := v_unversioned || TG_ARGV[i];" + "\n" +
        "  END LOOP;" + "\n" +
        "  v_delta := (SELECT coalesce(jsonb_object_agg(o.key, o.value), '{}'::jsonb)" + "\n" +
        "                FROM jsonb_each(v_old) AS o(key, value)" + "\n" +
        "               WHERE (v_new -> o.key) IS DISTINCT FROM o.value" + "\n" +
        "                 AND NOT (o.key = ANY (v_unversioned)));" + "\n" +
        "  IF v_delta = '{}'::jsonb THEN" + "\n" +
        "    RETURN NULL;" + "\n" +
        "  END IF;" + "\n" +
        "  v_op := CASE" + "\n" +
        "    WHEN v_old ->> TG_ARGV[1] IS NULL AND v_new ->> TG_ARGV[1] IS NOT NULL THEN 'soft_delete'" + "\n" +
        "    WHEN v_old ->> TG_ARGV[1] IS NOT NULL AND v_new ->> TG_ARGV[1] IS NULL THEN 'restore'" + "\n" +
        "    ELSE 'update' END;" + "\n" +
        "  IF TG_ARGV[2] <> '' THEN" + "\n" +
        "    v_tenant := (v_new ->> TG_ARGV[2])::uuid;" + "\n" +
        "  END IF;" + "\n" +
        "  v_actor := current_setting('";

    internal const string CaptureFunctionTail =
        "', true);" + "\n" +
        "  IF v_actor ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN" + "\n" +
        "    v_actor_kind := 'user';" + "\n" +
        "    v_actor_user := v_actor::uuid;" + "\n" +
        "  ELSIF v_actor = 'system' THEN" + "\n" +
        "    v_actor_kind := 'system';" + "\n" +
        "  ELSE" + "\n" +
        "    v_actor_kind := 'unknown';" + "\n" +
        "  END IF;" + "\n" +
        "  INSERT INTO row_history (tenant_id, source_table, row_id, op, old_values, actor_user_id, actor_kind, xact_id)" + "\n" +
        "  VALUES (v_tenant, TG_TABLE_NAME, (v_old ->> TG_ARGV[0])::uuid, v_op, v_delta, v_actor_user, v_actor_kind," + "\n" +
        "          pg_current_xact_id()::text::bigint);" + "\n" +
        "  RETURN NULL;" + "\n" +
        "END" + "\n" +
        "$rh$";
}
