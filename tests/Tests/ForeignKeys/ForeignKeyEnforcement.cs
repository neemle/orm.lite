using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys;

internal static class ForeignKeyEnforcement
{
    private static readonly Guid ParentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ChildId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Missing = Guid.Parse("00000000-0000-0000-0000-000000000003");

    internal static GeneratedModel Model(ReferentialAction action, string name = "fk_child_parent")
    {
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, true);
        var (diagnostics, _, output) = Run(ForeignKeyGeneration.Source(
            "[ForeignKey(" + literal + ", ReferentialAction." + action + ")]"));
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    internal static async Task VerifyAsync(ForeignKeyDatabase database, ReferentialAction action)
    {
        var model = Model(action);
        await database.Context.MigrateAsync(model.Contributions);
        var catalog = Assert.Single(await database.ForeignKeysAsync("a_children"));
        Assert.Equal("fk_child_parent", catalog.Name);
        Assert.Equal(["parent_id"], catalog.Columns);
        Assert.Equal("z_parents", catalog.ReferencedTable);
        Assert.Equal(["id"], catalog.ReferencedColumns);
        Assert.Equal(action, catalog.OnDelete);
        await DataAsync(database);
        await DeleteAsync(database, action);
        var history = await database.ScalarAsync("SELECT count(*) FROM schema_migrations");
        await database.Context.MigrateAsync(model.Contributions);
        Assert.Equal(history, await database.ScalarAsync("SELECT count(*) FROM schema_migrations"));
        Assert.Single(await database.ForeignKeysAsync("a_children"));
    }

    private static async Task DataAsync(ForeignKeyDatabase database)
    {
        await database.ExecuteAsync("INSERT INTO z_parents (id) VALUES (@p0)", ParentId);
        await database.ExecuteAsync("INSERT INTO a_children (id,parent_id) VALUES (@p0,@p1)", ChildId, ParentId);
        await database.ExecuteAsync("INSERT INTO a_children (id,parent_id) VALUES (@p0,@p1)", Guid.NewGuid(), null);
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(
            "INSERT INTO a_children (id,parent_id) VALUES (@p0,@p1)", Guid.NewGuid(), Missing));
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(
            "UPDATE a_children SET parent_id=@p0 WHERE id=@p1", Missing, ChildId));
        Assert.Equal(2L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM a_children")));
    }

    private static async Task DeleteAsync(ForeignKeyDatabase database, ReferentialAction action)
    {
        if (action is ReferentialAction.NoAction or ReferentialAction.Restrict)
        {
            await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(
                "DELETE FROM z_parents WHERE id=@p0", ParentId));
            Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM z_parents")));
            return;
        }
        await database.ExecuteAsync("DELETE FROM z_parents WHERE id=@p0", ParentId);
        var count = Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM a_children"));
        Assert.Equal(action == ReferentialAction.Cascade ? 1L : 2L, count);
        Assert.Equal(count, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM a_children WHERE parent_id IS NULL")));
    }

    internal static async Task QuotedNameAsync(ForeignKeyDatabase database)
    {
        const string name = "fk_\"; DROP TABLE z_parents;--🚀";
        await database.Context.MigrateAsync(Model(ReferentialAction.Restrict, name).Contributions);
        Assert.Equal(name, Assert.Single(await database.ForeignKeysAsync("a_children")).Name);
        Assert.Equal(0L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM z_parents")));
    }
}
