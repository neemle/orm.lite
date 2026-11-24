using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-31 (B45): the catalog is read by oid and by the key columns of an index, and a constraint is looked
// up as the unique or primary key it is.
public sealed class PostgresCatalogReadTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private OrmContext Ctx => fixture.Context;

    private async Task<T> WithCatalogAsync<T>(Func<ISchemaCatalog, Task<T>> read)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await read(Ctx.Provider.CreateCatalog(connection));
    }

    private async Task<IReadOnlyList<string>> ColumnsOfAsync(string name) =>
        (await ConstraintColumns.ForAsync(Ctx, name)).Columns;

    [SkippableFact(DisplayName = "a unique index with an INCLUDE column reports only its key columns, and so does "
        + "the lookup by its name")]
    public async Task An_include_column_is_not_a_key_column()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync("CREATE TABLE \"cat_inc\" (\"id\" text PRIMARY KEY, \"email\" text NOT NULL, "
            + "\"name\" text NOT NULL)");
        await fixture.ExecuteAsync("CREATE UNIQUE INDEX \"cat_inc_u\" ON \"cat_inc\" (\"email\") INCLUDE (\"name\")");

        var indexes = await WithCatalogAsync(c => c.IndexesAsync("cat_inc"));

        var index = Assert.Single(indexes);
        Assert.Equal(["email"], index.Terms);
        Assert.Contains("INCLUDE", index.Definition, StringComparison.Ordinal);
        Assert.Equal(["email"], await ColumnsOfAsync("cat_inc_u"));
    }

    [SkippableFact(DisplayName = "a primary key with an INCLUDE column gives key position 1 to its key column and none "
        + "to the included one")]
    public async Task An_included_column_has_no_key_position()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync("CREATE TABLE \"cat_pk\" (\"id\" text, \"extra\" text, "
            + "PRIMARY KEY (\"id\") INCLUDE (\"extra\"))");

        var columns = await WithCatalogAsync(c => c.ColumnsAsync("cat_pk"));

        Assert.Equal([("id", 1), ("extra", 0)], columns.Select(c => (c.Name, c.KeyPosition)));
    }

    [SkippableFact(DisplayName = "a unique constraint is read through its key columns, and a check constraint or a "
        + "foreign key of the same schema is no unique constraint")]
    public async Task Only_a_unique_or_primary_constraint_has_collision_columns()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync("CREATE TABLE \"cat_con\" (\"id\" text PRIMARY KEY, \"email\" text NOT NULL, "
            + "\"name\" text NOT NULL, \"owner\" text REFERENCES \"cat_con\" (\"id\"))");
        await fixture.ExecuteAsync("ALTER TABLE \"cat_con\" ADD CONSTRAINT \"cat_con_uc\" UNIQUE (\"name\") "
            + "INCLUDE (\"email\")");
        await fixture.ExecuteAsync("ALTER TABLE \"cat_con\" ADD CONSTRAINT \"cat_con_chk\" CHECK (\"email\" <> '')");
        await fixture.ExecuteAsync("ALTER TABLE \"cat_con\" ADD CONSTRAINT \"cat_con_fk\" FOREIGN KEY (\"email\") "
            + "REFERENCES \"cat_con\" (\"id\")");

        Assert.Equal(["name"], await ColumnsOfAsync("cat_con_uc"));
        Assert.Equal(["id"], await ColumnsOfAsync("cat_con_pkey"));
        Assert.Empty(await ColumnsOfAsync("cat_con_chk"));
        Assert.Empty(await ColumnsOfAsync("cat_con_fk"));
    }

    [SkippableFact(DisplayName = "a foreign key whose parent lives in a schema that is not on the search path reports "
        + "the parent's own columns")]
    public async Task A_parent_in_another_schema_is_read_by_oid()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync("CREATE SCHEMA \"cat_away\"");
        await fixture.ExecuteAsync(
            "CREATE TABLE \"cat_away\".\"cat_parent\" (\"pid\" text PRIMARY KEY, \"label\" text)");
        await fixture.ExecuteAsync("CREATE TABLE \"cat_child\" (\"id\" text PRIMARY KEY, \"parent_ref\" text "
            + "REFERENCES \"cat_away\".\"cat_parent\" (\"pid\"))");

        var keys = await WithCatalogAsync(c => c.ForeignKeysAsync("cat_child"));

        var key = Assert.Single(keys);
        Assert.Equal("cat_parent", key.ReferencedTable);
        Assert.Equal(["parent_ref"], key.Columns);
        Assert.Equal(["pid"], key.ReferencedColumns);
    }

    [SkippableFact(DisplayName = "a same-named table earlier on the search path does not stand in for the parent: the "
        + "columns come from the table the key points at")]
    public async Task A_same_named_table_on_the_search_path_is_not_the_parent()
    {
        fixture.RequireAvailable();
        await fixture.ExecuteAsync("CREATE TABLE \"cat_twin\" (\"hid\" text PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE SCHEMA \"cat_other\"");
        await fixture.ExecuteAsync("CREATE TABLE \"cat_other\".\"cat_twin\" (\"zid\" text PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE \"cat_kid\" (\"id\" text PRIMARY KEY, \"twin\" text "
            + "REFERENCES \"cat_other\".\"cat_twin\" (\"zid\"))");

        var keys = await WithCatalogAsync(c => c.ForeignKeysAsync("cat_kid"));

        Assert.Equal(["zid"], Assert.Single(keys).ReferencedColumns);
    }
}
