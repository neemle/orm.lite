using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class ReconcileLoweringTests
{
    private sealed class WithoutCatalog : ISqlProvider
    {
        public string Id => "bare";

        public CapabilitySet Capabilities { get; } = CapabilitySet.Everything().With(Capability.CatalogGuard, Support.None);

        public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();

        public Node Lower(Node tree) => PostgresProvider.Instance.Lower(tree);
    }

    // ORML-30 (B43/B44/B48): the drift block looks the index up through its own table, refuses a name another
    // relation holds, escapes names through PostgresNames and strips the exact qualified table text.
    private const string ExpectedDrift =
        "DO $drift$ DECLARE current_index oid; BEGIN IF to_regclass('\"docs\"') IS NOT NULL AND (SELECT count(*) "
        + "FROM pg_attribute WHERE attrelid = to_regclass('\"docs\"') AND attnum > 0 AND NOT attisdropped "
        + "AND attname = ANY (ARRAY['a', 'b'])) = 2 THEN "
        + "SELECT ic.oid INTO current_index FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid "
        + "WHERE i.indrelid = to_regclass('\"docs\"') AND ic.relname = 'ix_a'; "
        + "IF current_index IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = 'ix_a' "
        + "AND x.relnamespace = (SELECT t.relnamespace FROM pg_class t WHERE t.oid = to_regclass('\"docs\"'))) "
        + "THEN RAISE EXCEPTION '" + PostgresDdlDataLiterals.IndexNameTakenMessage + "', 'ix_a', 'docs'; END IF; "
        + "IF current_index IS NOT NULL THEN CREATE TEMP TABLE \"orm_ix_check\" (LIKE \"docs\"); "
        + "EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS \"ix_a\" ON \"orm_ix_check\" (\"a\", \"b\")'; "
        + "IF replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.', "
        + "quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "WHERE c.oid = to_regclass('\"docs\"')), ' USING '), ' USING ') <> "
        + "regexp_replace(pg_get_indexdef(to_regclass('pg_temp.\"ix_a\"')), E' ON \\\\S+ USING ', ' USING ') "
        + "THEN DROP TABLE pg_temp.\"orm_ix_check\"; EXECUTE 'DROP INDEX ' || current_index::regclass; "
        + "ELSE DROP TABLE pg_temp.\"orm_ix_check\"; END IF; END IF; END IF; END $drift$";

    private static ReconcileIndexes One(string index, string table, bool live, params string[] columns) =>
        new([new ReconcileIndex(
            new CreateIndex(index, table, NodeList.Of(columns.Select(c => (Node)new ColumnRef(c))), true, live ? new Where([new IsNull(new ColumnRef("deleted_at"))]) : null),
            NodeList.Of(columns.Select(c => (Node)new ColumnRef(c))),
            live ? NodeList.Of(columns.Select(c => (Node)new ColumnRef(c))) : null)]);

    [Fact(DisplayName = "a reconcile set lowers to the lock timeout, then per index one guard block that creates the index only when the table and every guarded column exist")]
    public void The_guard_block_creates_the_index_when_the_columns_exist()
    {
        var statements = DdlCompiler.Render([One("ix_a", "docs", false, "a", "b")], PostgresProvider.Instance);

        Assert.Equal(
            new[]
            {
                "SET LOCAL lock_timeout = '5s'",
                ExpectedDrift,
                "DO $reconcile$ BEGIN IF to_regclass('\"docs\"') IS NOT NULL AND (SELECT count(*) FROM pg_attribute WHERE attrelid = to_regclass('\"docs\"') AND attnum > 0 AND NOT attisdropped AND attname = ANY (ARRAY['a', 'b'])) = 2 "
                + "THEN EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS \"ix_a\" ON \"docs\" (\"a\", \"b\")'; END IF; END $reconcile$",
            },
            statements);
    }

    [Fact(DisplayName = "a live-only item adds one collision block naming the live index, the table and the key columns, with every name quoted into the message")]
    public void A_live_only_item_adds_the_collision_block()
    {
        var statements = DdlCompiler.Render([One("ix_live", "docs", true, "a")], PostgresProvider.Instance);

        Assert.Equal(4, statements.Count);
        Assert.StartsWith("DO $collision$ DECLARE r record; BEGIN IF to_regclass('\"docs\"') IS NOT NULL", statements[3], StringComparison.Ordinal);
        Assert.Contains("'ix_live', 'docs', 'a', CASE WHEN r.con IS NULL THEN 'index' ELSE 'constraint' END", statements[3], StringComparison.Ordinal);
        Assert.EndsWith("END LOOP; END IF; END $collision$", statements[3], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a name with a single quote is doubled in the message literals and in the executed statement, so it cannot end the literal")]
    public void A_quote_in_a_name_is_doubled()
    {
        var statements = DdlCompiler.Render([One("ix'live", "do'cs", true, "a'b")], PostgresProvider.Instance);

        Assert.Contains("'ix''live', 'do''cs', 'a''b'", statements[3], StringComparison.Ordinal);
        Assert.Contains("ARRAY['a''b']", statements[2], StringComparison.Ordinal);
        Assert.Contains("EXECUTE 'CREATE UNIQUE INDEX IF NOT EXISTS \"ix''live\"", statements[2], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an empty reconcile set is the lock timeout alone")]
    public void An_empty_set_is_the_lock_timeout()
    {
        Assert.Equal(new[] { "SET LOCAL lock_timeout = '5s'" }, DdlCompiler.Render([new ReconcileIndexes([])], PostgresProvider.Instance));
    }

    [Fact(DisplayName = "a provider without the catalog-guard capability refuses a reconcile set by name before any text is written")]
    public void A_provider_without_the_capability_refuses()
    {
        var refusal = Assert.Throws<CapabilityNotSupportedException>(() => DdlCompiler.Render([One("ix_a", "docs", false, "a")], new WithoutCatalog()));

        Assert.Equal(Capability.CatalogGuard, refusal.Capability);
        Assert.Equal("bare", refusal.Provider);
        Assert.Equal(nameof(ReconcileIndexes), refusal.Node);
    }

    [Fact(DisplayName = "the reconcile nodes reach the standard serializer only after lowering")]
    public void The_nodes_must_be_lowered()
    {
        var serializer = PostgresProvider.Instance.CreateSerializer();

        Assert.Throws<NotSupportedException>(() => serializer.Serialize(One("ix_a", "docs", false, "a")));
    }
}
