using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.HostileNames;

public sealed class HostileNameRenderingTests
{
    // The probe side keeps the regular expression; a string with a backslash is written as an escape string.
    private const string Pattern = "E' ON \\\\S+ USING '";

    private static ReconcileIndexes Items(string index, string table, bool live, string[] columns)
    {
        var terms = NodeList.Of(columns.Select(c => (Node)new ColumnRef(c)));
        var filter = live ? new Where([new IsNull(new ColumnRef("deleted_at"))]) : null;
        var create = new CreateIndex(index, table, terms, true, filter);
        return new([new ReconcileIndex(create, terms, live ? terms : null)]);
    }

    private static IReadOnlyList<string> Render(string index, string table, bool live = false, string column = "a") =>
        DdlCompiler.Render([Items(index, table, live, [column])], PostgresProvider.Instance);

    private static ConvertToLiveOnlyIndex LiveOnly(string table, string total, string live, bool revert) => new(
        table, NodeList.Of([new ColumnRef("a")]), "deleted_at", total, live, false, "v1", revert);

    private static void ClosesOnlyAtTheEnd(string statement)
    {
        var tag = statement[3..(statement.IndexOf('$', 4) + 1)];

        var firstClose = statement.IndexOf(tag, tag.Length + 3, StringComparison.Ordinal);

        Assert.Equal(statement.Length - tag.Length, firstClose);
    }

    [Fact(DisplayName = "[SEC] a double quote in a table name is doubled in every regclass literal and probe "
        + "statement, so to_regclass never receives a broken identifier")]
    public void A_double_quote_in_a_table_name_is_doubled()
    {
        var statements = Render("ix_a", "we\"ird");

        var all = string.Join("\n", statements);
        Assert.Contains("to_regclass('\"we\"\"ird\"')", all, StringComparison.Ordinal);
        Assert.DoesNotContain("to_regclass('\"we\"ird\"')", all, StringComparison.Ordinal);
        Assert.Contains("(LIKE \"we\"\"ird\")", statements[1], StringComparison.Ordinal);
        Assert.Contains(" ON \"we\"\"ird\" (\"a\")'", statements[2], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[SEC] a single quote in an index name is doubled in the lookup, the probe index, the refusal "
        + "arguments and the executed statement, so it cannot end a literal")]
    public void A_single_quote_in_an_index_name_cannot_end_a_literal()
    {
        var drift = Render("ix'a", "docs")[1];

        Assert.Contains("ic.relname = 'ix''a'", drift, StringComparison.Ordinal);
        Assert.Contains("to_regclass('pg_temp.\"ix''a\"')", drift, StringComparison.Ordinal);
        Assert.Contains("', 'ix''a', 'docs';", drift, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS \"ix''a\" ON \"orm_ix_check\"", drift, StringComparison.Ordinal);
        Assert.DoesNotContain("'ix'a'", drift, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[SEC] a single quote in a table name is doubled in the collision block of a live-only index")]
    public void A_single_quote_in_a_table_name_is_doubled_in_the_collision_block()
    {
        var collision = Render("ix_live", "do'cs", true)[3];

        Assert.Contains("i.indrelid = to_regclass('\"do''cs\"')", collision, StringComparison.Ordinal);
        Assert.Contains("'ix_live', 'do''cs', 'a'", collision, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a percent sign in a name is doubled in the owner-constraint format template, the recreate "
        + "message and the soft-column message, so the placeholders keep their count")]
    public void A_percent_sign_in_a_name_is_doubled()
    {
        var terms = NodeList.Of([new ColumnRef("a")]);
        var up = new ConvertToLiveOnlyIndex("t%t", terms, "deleted_at", "ix_total", "ix_live", false, "v1", false);
        var down = new ConvertToLiveOnlyIndex("t%t", terms, "deleted_at", "ix%total", "ix_live", false, "v1", true);

        Assert.Contains("ALTER TABLE \"t%%t\" DROP CONSTRAINT %I", DdlText.Of(up), StringComparison.Ordinal);
        Assert.Contains(
            "cannot recreate unique index ix%%total on t%%t (a): duplicate key % exists",
            DdlText.Of(down),
            StringComparison.Ordinal);
        Assert.Contains(
            "cannot drop deleted_at from t%%t: % soft-deleted",
            DdlText.Of(new SoftDeleteColumn("t%t", "v1", true)),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a table name with whitespace is compared by its exact qualified text, so the regular "
        + "expression that cannot span whitespace stays on the probe side only")]
    public void A_table_name_with_whitespace_is_stripped_by_its_exact_text()
    {
        var drift = Render("ix_a", "my docs")[1];

        Assert.Contains("to_regclass('\"my docs\"')", drift, StringComparison.Ordinal);
        Assert.Contains(
            "replace(pg_get_indexdef(current_index), concat(' ON ', (SELECT concat(quote_ident(n.nspname), '.', "
            + "quote_ident(c.relname)) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
            + "WHERE c.oid = to_regclass('\"my docs\"')), ' USING '), ' USING ')",
            drift,
            StringComparison.Ordinal);
        Assert.Equal(1, drift.Split(Pattern).Length - 1);
    }

    [Fact(DisplayName = "[SEC] a name that holds the dollar tag of its block moves the block to a tag the body does "
        + "not hold, so the dollar quote closes only at the end")]
    public void A_name_that_holds_the_tag_cannot_close_the_block()
    {
        var once = Render("a$drift$b", "docs")[1];
        var twice = Render("a$drift$b$driftx$c", "docs")[1];
        var reconcile = Render("a$reconcile$b", "docs")[2];

        Assert.StartsWith("DO $driftx$ DECLARE", once, StringComparison.Ordinal);
        Assert.StartsWith("DO $driftxx$ DECLARE", twice, StringComparison.Ordinal);
        Assert.StartsWith("DO $reconcilex$ BEGIN", reconcile, StringComparison.Ordinal);
        Assert.All(new[] { once, twice, reconcile }, statement => ClosesOnlyAtTheEnd(statement));
    }

    [Fact(DisplayName = "an ordinary name keeps the tag the block always had")]
    public void An_ordinary_name_keeps_its_tag()
    {
        var statements = Render("ix_a", "docs");

        Assert.StartsWith("DO $drift$ DECLARE", statements[1], StringComparison.Ordinal);
        Assert.StartsWith("DO $reconcile$ BEGIN", statements[2], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the existing-tables lookup accepts only an ordinary or a partitioned table, "
        + "not a view, a sequence or an index")]
    public void The_existing_tables_query_checks_the_relation_kind()
    {
        var text = DdlText.Statement(new ExistingTablesQuery(new Param(0, new[] { "t" }, "tables"))).Text;

        Assert.Equal(
            "SELECT t FROM unnest(@tables::text[]) AS t WHERE EXISTS (SELECT 1 FROM pg_class c "
            + "WHERE c.oid = to_regclass(quote_ident(t)) AND c.relkind IN ('p', 'r'))",
            text);
    }

    [Fact(DisplayName = "[SEC] the collision block escapes a double quote in the table, a single quote in the index "
        + "and a single quote in the key column, in the lookup, the key array and the message arguments")]
    public void The_collision_block_escapes_every_name()
    {
        var collision = Render("ix'live", "we\"ird", true, "a'b")[3];

        Assert.Contains("i.indrelid = to_regclass('\"we\"\"ird\"')", collision, StringComparison.Ordinal);
        Assert.Contains("= ANY (ARRAY['a''b'])", collision, StringComparison.Ordinal);
        Assert.Contains("'ix''live', 'we\"ird', 'a''b'", collision, StringComparison.Ordinal);
        Assert.DoesNotContain("'a'b'", collision, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a percent sign in a key column stays a plain argument of the collision block, never "
        + "a placeholder of its message")]
    public void A_percent_sign_in_a_key_column_is_an_argument_of_the_collision_block()
    {
        var collision = Render("ix_live", "docs", true, "a%b")[3];

        Assert.Contains("ARRAY['a%b']", collision, StringComparison.Ordinal);
        Assert.Contains("'ix_live', 'docs', 'a%b'", collision, StringComparison.Ordinal);
        Assert.DoesNotContain("a%%b", collision, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a percent sign in a plain or a lowered key column is doubled in the recreate message")]
    public void A_percent_sign_in_a_key_column_is_doubled_in_the_recreate_message()
    {
        var lowered = new Call(Fn.Lower, NodeList.Of([new ColumnRef("c%d")]));
        var down = new ConvertToLiveOnlyIndex(
            "t", NodeList.Of([new ColumnRef("a%b"), lowered]), "deleted_at", "ix_total", "ix_live", false, "v1", true);

        var text = DdlText.Of(down);

        Assert.Contains("on t (a%%b, lower(c%%d)): duplicate key % exists", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the live-only blocks find the total and the live index through their own table and drop "
        + "them by oid, never by a bare name")]
    public void The_live_only_blocks_resolve_both_indexes_through_their_table()
    {
        const string lookup = "FROM pg_index i JOIN pg_class ic ON ic.oid = i.indexrelid "
            + "WHERE i.indrelid = to_regclass('\"t\"') AND ic.relname = ";
        var up = DdlText.Of(LiveOnly("t", "ix_total", "ix_live", false));
        var down = DdlText.Of(LiveOnly("t", "ix_total", "ix_live", true));

        Assert.Contains("SELECT ic.oid INTO total_index " + lookup + "'ix_total';", up, StringComparison.Ordinal);
        Assert.Contains("SELECT ic.oid INTO live_index " + lookup + "'ix_live';", up, StringComparison.Ordinal);
        Assert.Contains("c.conindid = total_index AND", up, StringComparison.Ordinal);
        Assert.Contains("EXECUTE 'DROP INDEX ' || total_index::regclass", up, StringComparison.Ordinal);
        Assert.Contains("obj_description(live_index, 'pg_class')", down, StringComparison.Ordinal);
        Assert.Contains("EXECUTE 'DROP INDEX ' || live_index::regclass", down, StringComparison.Ordinal);
        foreach (var text in new[] { up, down })
            Assert.DoesNotContain("to_regclass('\"ix_", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP INDEX \"", up + down, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[SEC] the live-only blocks refuse a live index name in Up and a total index name in Down "
        + "that another relation holds, with the names escaped")]
    public void The_live_only_blocks_refuse_a_name_another_relation_holds()
    {
        var up = DdlText.Of(LiveOnly("t", "ix_total", "ix'live", false));
        var down = DdlText.Of(LiveOnly("t", "ix'total", "ix_live", true));

        const string held = " IS NULL AND EXISTS (SELECT 1 FROM pg_class x WHERE x.relname = ";
        Assert.Contains("IF live_index" + held + "'ix''live'", up, StringComparison.Ordinal);
        Assert.Contains("IF total_index" + held + "'ix''total'", down, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION 'Index name % is already held", up, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION 'Index name % is already held", down, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the table lookup of the catalog accepts only an ordinary or a partitioned table: "
        + "the statement filters the relation kind to r and p")]
    public void The_catalog_table_lookup_filters_the_relation_kind()
    {
        var text = DdlText.Statement(PostgresCatalog.TableQuery("t")).Text;

        Assert.Equal(
            "SELECT EXISTS (SELECT 1 FROM pg_class c WHERE c.oid = to_regclass('\"t\"') "
            + "AND c.relkind IN ('r', 'p'))",
            text);
    }
}
