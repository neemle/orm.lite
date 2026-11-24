using System.Data.Common;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresHostileNameTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private OrmContext Ctx => fixture.Context;

    private static EntityDescriptor Items(string table, string index) =>
        EntityDescriptor.Of("Fixture." + table, table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Status", "status", ColumnType.Text, false, false)
            .Index(index, true, false, IndexTerm.Asc("status"));

    private Task ApplyAsync(params Node[] operations) =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, operations);

    private Task ReconcileAsync(EntityDescriptor entity) =>
        ApplyAsync(new ReconcileIndexes(NodeList.Of(EntityStatements.ReconcileIndexes(entity))));

    private async Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await Ctx.Provider.CreateCatalog(connection).IndexesAsync(table);
    }

    private async Task<bool> TableExistsAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await Ctx.Provider.CreateCatalog(connection).TableExistsAsync(table);
    }

    private async Task<string> IndexOidAsync(string index)
    {
        var rows = await fixture.RowsAsync(
            "SELECT oid::text FROM pg_class WHERE relkind = 'i' AND relname = " + PostgresNames.Text(index).Text);
        return Convert.ToString(rows[0][0])!;
    }

    [SkippableTheory(DisplayName = "[SEC] a table and an index named with a quote, a percent sign, whitespace "
        + "or a dollar tag reconcile, and a second reconcile keeps the very same index")]
    [InlineData("we\"ird", "ix_dq")]
    [InlineData("it's", "ix'sq")]
    [InlineData("pc%t", "ix%pc")]
    [InlineData("my docs", "ix my docs")]
    [InlineData("tag$drift$t", "ix$reconcile$x")]
    public async Task A_hostile_name_reconciles_and_is_not_rebuilt(string table, string index)
    {
        fixture.RequireAvailable();
        var items = Items(table, index);
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        await ReconcileAsync(items);
        var first = await IndexOidAsync(index);
        await ReconcileAsync(items);

        Assert.Equal([index], (await IndexesAsync(table)).Select(i => i.Name));
        Assert.Equal(first, await IndexOidAsync(index));
        Assert.True(await TableExistsAsync(table));
    }

    // ORML-29 (B42): a name beyond the 63 bytes the server keeps is now refused before it is sent (see
    // PostgresIdentifierLimitTests), so this test of the lookup keeps its purpose at the longest name the server keeps.
    [SkippableFact(DisplayName = "an index name of the longest length the server keeps is found again, so it is not "
        + "rebuilt on every run")]
    public async Task A_name_of_the_longest_kept_length_is_found_again()
    {
        fixture.RequireAvailable();
        var name = "ix_hn_long_" + new string('l', 52);
        var items = Items("hn_long", name);
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        await ReconcileAsync(items);
        var first = await IndexOidAsync(name);
        await ReconcileAsync(items);

        Assert.Equal(first, await IndexOidAsync(name));
        Assert.Single(await IndexesAsync("hn_long"));
    }

    [SkippableFact(DisplayName = "[SEC] an index name that another table of the schema already holds is refused by "
        + "name, and the first table keeps its own index untouched")]
    public async Task A_name_held_by_another_table_is_refused_and_nothing_is_dropped()
    {
        fixture.RequireAvailable();
        var first = Items("hn_first", "hn_shared");
        var second = Items("hn_second", "hn_shared");
        await ApplyAsync([.. EntityStatements.CreateTables(first), .. EntityStatements.CreateTables(second)]);
        await ReconcileAsync(first);
        var before = await IndexOidAsync("hn_shared");

        var refusal = await Assert.ThrowsAnyAsync<DbException>(() => ReconcileAsync(second));
        await ReconcileAsync(first);

        Assert.Contains("hn_shared", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("schema-global", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await IndexOidAsync("hn_shared"));
        Assert.Equal(["hn_shared"], (await IndexesAsync("hn_first")).Select(i => i.Name));
        Assert.Empty(await IndexesAsync("hn_second"));
    }

    [SkippableFact(DisplayName = "a view, a materialized view, a sequence and an index named like a table are not "
        + "tables, and a partitioned table is one, to the catalog and to the existing-tables lookup")]
    public async Task Only_a_table_is_a_table()
    {
        fixture.RequireAvailable();
        var items = Items("hn_kind", "hn_kind_ix");
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);
        await ReconcileAsync(items);
        await fixture.ExecuteAsync("CREATE VIEW \"hn_kind_view\" AS SELECT \"id\" FROM \"hn_kind\"");
        await fixture.ExecuteAsync("CREATE MATERIALIZED VIEW \"hn_kind_mv\" AS SELECT \"id\" FROM \"hn_kind\"");
        await fixture.ExecuteAsync("CREATE SEQUENCE \"hn_kind_seq\"");
        await fixture.ExecuteAsync("CREATE TABLE \"hn_kind_part\" (\"id\" text) PARTITION BY LIST (\"id\")");
        string[] names = ["hn_kind", "hn_kind_part", "hn_kind_view", "hn_kind_mv", "hn_kind_ix", "hn_kind_seq", "none"];

        foreach (var table in new[] { "hn_kind", "hn_kind_part" }) Assert.True(await TableExistsAsync(table));
        foreach (var other in names.Skip(2)) Assert.False(await TableExistsAsync(other));
        Assert.Equal(["hn_kind", "hn_kind_part"], await ExistingAsync(names));
    }

    private async Task<List<string>> ExistingAsync(string[] names)
    {
        var query = HistoryStatements.ExistingTables(names);
        var statement = StatementCompiler.Compile(query, Ctx.Provider, StatementKind.Read);
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = statement.Text;
        var parameter = command.CreateParameter();
        parameter.ParameterName = statement.Parameters[0].Name;
        parameter.Value = names;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        var found = new List<string>();
        while (await reader.ReadAsync()) found.Add(reader.GetString(0));
        return found;
    }

    private async Task<string> TableOfIndexAsync(string index)
    {
        var rows = await fixture.RowsAsync(
            "SELECT t.relname FROM pg_index i JOIN pg_class t ON t.oid = i.indrelid JOIN pg_class x "
            + "ON x.oid = i.indexrelid WHERE x.relname = " + PostgresNames.Text(index).Text);
        return rows.Count == 0 ? "" : Convert.ToString(rows[0][0])!;
    }

    private async Task CreateLiveTablesAsync(params string[] tables)
    {
        foreach (var table in tables)
            await fixture.ExecuteAsync(
                "CREATE TABLE \"" + table + "\" (\"id\" text PRIMARY KEY, \"name\" text, "
                + "\"deleted_at\" timestamptz NULL)");
    }

    [SkippableFact(DisplayName = "[SEC] a live-only conversion whose total index name belongs to another table leaves "
        + "that table's index alone and only creates its own live index")]
    public async Task A_total_index_of_another_table_is_not_dropped()
    {
        fixture.RequireAvailable();
        await CreateLiveTablesAsync("lo_a", "lo_b");
        await fixture.ExecuteAsync("CREATE UNIQUE INDEX \"lo_total\" ON \"lo_b\" (\"name\")");
        var up = LiveOnlyConversionDdl.Up("lo_a", ["name"], "deleted_at", "lo_total", "lo_live_a", false, "v1");

        await ApplyAsync(up);

        Assert.Equal("lo_b", await TableOfIndexAsync("lo_total"));
        Assert.Equal("lo_a", await TableOfIndexAsync("lo_live_a"));
    }

    [SkippableFact(DisplayName = "[SEC] a live-only conversion whose live index name belongs to another table is "
        + "refused by name and drops nothing")]
    public async Task A_live_index_name_of_another_table_is_refused()
    {
        fixture.RequireAvailable();
        await CreateLiveTablesAsync("lo_c", "lo_d");
        await fixture.ExecuteAsync("CREATE UNIQUE INDEX \"lo_total_c\" ON \"lo_c\" (\"name\")");
        await fixture.ExecuteAsync("CREATE UNIQUE INDEX \"lo_live_x\" ON \"lo_d\" (\"name\")");
        var up = LiveOnlyConversionDdl.Up("lo_c", ["name"], "deleted_at", "lo_total_c", "lo_live_x", false, "v1");

        var refusal = await Assert.ThrowsAnyAsync<DbException>(() => ApplyAsync(up));

        Assert.Contains("already held", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("lo_c", await TableOfIndexAsync("lo_total_c"));
        Assert.Equal("lo_d", await TableOfIndexAsync("lo_live_x"));
    }

    [SkippableFact(DisplayName = "a percent sign in a table name reaches the server doubled, so the refusal to drop "
        + "a soft-delete column names the table and counts its rows")]
    public async Task A_percent_sign_in_a_table_name_keeps_the_refusal_readable()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync(
            "CREATE TABLE \"pc%soft\" (\"id\" text PRIMARY KEY, \"deleted_at\" timestamptz NULL)");
        await fixture.ExecuteAsync("INSERT INTO \"pc%soft\" (\"id\", \"deleted_at\") VALUES ('a', now())");
        await fixture.ExecuteAsync("COMMENT ON COLUMN \"pc%soft\".\"deleted_at\" IS 'v1'");
        var drop = new SoftDeleteColumn("pc%soft", "v1", true);

        var refusal = await Assert.ThrowsAnyAsync<DbException>(() => ApplyAsync(drop));

        Assert.Contains(
            "cannot drop deleted_at from pc%soft: 1 soft-deleted row(s) would read as live",
            refusal.Message,
            StringComparison.Ordinal);
    }
}
