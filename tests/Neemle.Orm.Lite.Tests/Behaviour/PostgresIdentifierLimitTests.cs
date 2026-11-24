using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-29 (B42): on a real server two index names that agree in their first 63 bytes would have been cut
// to one name, and the second CREATE INDEX IF NOT EXISTS skipped with a NOTICE. They are refused before the
// server is asked.
public sealed class PostgresIdentifierLimitTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private OrmContext Ctx => fixture.Context;

    private static EntityDescriptor Items(string table, params string[] indexes)
    {
        var entity = EntityDescriptor.Of("Fixture." + table, table, true)
            .Property("Id", "id", ColumnType.Text, false, true)
            .Property("Status", "status", ColumnType.Text, false, false)
            .Property("Kind", "kind", ColumnType.Text, false, false);
        for (var i = 0; i < indexes.Length; i++)
            entity.Index(indexes[i], true, false, IndexTerm.Asc(i == 0 ? "status" : "kind"));
        return entity;
    }

    private Task ApplyAsync(params Node[] operations) =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, operations);

    private Task ReconcileAsync(EntityDescriptor entity) =>
        ApplyAsync(new ReconcileIndexes(NodeList.Of(EntityStatements.ReconcileIndexes(entity))));

    private async Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        return await Ctx.Provider.CreateCatalog(connection).IndexesAsync(table);
    }

    [SkippableFact(DisplayName = "two unique indexes whose names share their first 63 bytes are refused by name "
        + "and neither is created")]
    public async Task Names_that_the_server_would_cut_to_one_are_refused()
    {
        fixture.RequireAvailable();
        var prefix = "ix_idl_" + new string('p', 56);
        var items = Items("idl_collide", prefix + "_one", prefix + "_two");
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        var refusal = await Assert.ThrowsAsync<IdentifierTooLongException>(() => ReconcileAsync(items));

        Assert.Equal(prefix + "_one", refusal.Name);
        Assert.Empty(await IndexesAsync("idl_collide"));
    }

    [SkippableFact(DisplayName = "an index named with exactly 63 bytes is created under that very name, and a "
        + "second reconcile keeps it")]
    public async Task A_name_of_63_bytes_is_kept_whole()
    {
        fixture.RequireAvailable();
        var name = "ix_idl_" + new string('k', 56);
        var items = Items("idl_exact", name);
        await ApplyAsync([.. EntityStatements.CreateTables(items)]);

        await ReconcileAsync(items);
        await ReconcileAsync(items);

        Assert.Equal(63, name.Length);
        Assert.Equal([name], (await IndexesAsync("idl_exact")).Select(i => i.Name));
    }

    [SkippableFact(DisplayName = "a table with a column named beyond 63 bytes is refused by name and the table "
        + "is not created")]
    public async Task A_long_column_name_is_refused_before_the_server_is_asked()
    {
        fixture.RequireAvailable();
        var key = new ColumnDef("id", new TypeRef(ColumnType.Text), false, true);
        var wide = new ColumnDef(new string('c', 64), new TypeRef(ColumnType.Text));

        var refusal = await Assert.ThrowsAsync<IdentifierTooLongException>(
            () => ApplyAsync(new CreateTable("idl_wide", [key, wide])));

        Assert.Equal("column", refusal.Kind);
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        Assert.False(await Ctx.Provider.CreateCatalog(connection).TableExistsAsync("idl_wide"));
    }
}
