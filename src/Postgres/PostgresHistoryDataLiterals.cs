namespace Neemle.OrmLite.Postgres;

internal static class PostgresHistoryDataLiterals
{
    internal const string StateHead =
        "SELECT (to_jsonb(live.*) || coalesce((SELECT jsonb_object_agg(x.k, x.v) FROM (SELECT DISTINCT ON (e.k) e.k, e.v"
        + " FROM row_history h CROSS JOIN LATERAL jsonb_each(h.old_values) AS e(k, v) WHERE h.source_table =";

    internal const string StateRowId = "AND h.row_id =";

    internal const string StateVersion = "AND h.id >=";

    internal const string StateMerged = "ORDER BY e.k, h.id) x), '{}'::jsonb)) -";

    internal const string StateVersionExists =
        "AND EXISTS (SELECT 1 FROM row_history v WHERE v.id =";

    internal const string StateVersionTable = "AND v.source_table =";

    internal const string StateVersionRow = "AND v.row_id =";

    internal const string RevertHead = "WITH st(state) AS";

    internal const string RevertTarget = "UPDATE";

    internal const string RevertAlias = "AS t SET";

    internal const string RevertSource = "FROM st CROSS JOIN LATERAL";

    internal const string RevertRecord = "st.state";

    internal const string SoftDeleteHead =
        "SELECT p.t, p.id, h.actor_user_id, h.actor_kind, h.xact_id, h.changed_at FROM";

    internal const string SoftDeleteTail =
        "AS p(t, id) CROSS JOIN LATERAL (SELECT actor_user_id, actor_kind, xact_id, changed_at FROM row_history x"
        + " WHERE x.source_table = p.t AND x.row_id = p.id AND x.op = 'soft_delete' ORDER BY x.id DESC LIMIT 1) h";

    internal const string ConstraintHead =
        "SELECT a.attname FROM pg_constraint k CROSS JOIN LATERAL unnest(k.conkey) WITH ORDINALITY AS c(attnum, ord)"
        + " JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = c.attnum WHERE k.conname =";

    internal const string ConstraintTail =
        "AND k.contype IN ('p', 'u', 'x') AND pg_table_is_visible(k.conrelid) ORDER BY c.ord";

    internal const string IndexHead =
        "SELECT a.attname FROM pg_class r JOIN pg_index i ON i.indexrelid = r.oid CROSS JOIN LATERAL unnest(i.indkey::int2[])"
        + " WITH ORDINALITY AS c(attnum, ord) JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = c.attnum"
        + " WHERE r.relname =";

    internal const string IndexTail =
        "AND r.relkind = 'i' AND pg_table_is_visible(r.oid) AND c.attnum > 0 AND c.ord <= i.indnkeyatts ORDER BY c.ord";

    internal const string ChangedKeysAlias = "k";

    internal const string TextArray = "text[]";

    internal const string UuidArray = "uuid[]";
}
