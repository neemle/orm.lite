using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Final;

internal static class FinalLinkWitness
{
    internal static GeneratedModel Model(LinkForeignKeyEnd end)
    {
        var source = $$"""
            using Neemle.OrmLite.Nodes;
            [Table("final_users")] public class FinalUser {
                public Guid Id { get; set; }
                [ManyToMany("final_links", "user_id", "role_id")]
                [LinkForeignKey(LinkForeignKeyEnd.{{end}}, "fk_final_one", ReferentialAction.Cascade)]
                public System.Collections.Generic.List<FinalRole> Roles { get; set; } = new();
            }
            [Table("final_roles")] public class FinalRole { public Guid Id { get; set; } }
            """;
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    internal static async Task VerifyAsync(ForeignKeyDatabase database, LinkForeignKeyEnd end)
    {
        await database.Context.MigrateAsync(Model(end).Contributions);
        var key = Assert.Single(await database.ForeignKeysAsync("final_links"));
        Assert.Equal("fk_final_one", key.Name);
        Assert.Equal([end == LinkForeignKeyEnd.Source ? "user_id" : "role_id"], key.Columns);
        Assert.Equal(end == LinkForeignKeyEnd.Source ? "final_users" : "final_roles", key.ReferencedTable);
        Assert.Equal(["id"], key.ReferencedColumns);
        Assert.Equal(ReferentialAction.Cascade, key.OnDelete);
        await VerifyDataAsync(database, end);
    }

    private static async Task VerifyDataAsync(ForeignKeyDatabase database, LinkForeignKeyEnd end)
    {
        var user = Guid.NewGuid();
        var role = Guid.NewGuid();
        await database.ExecuteAsync("INSERT INTO final_users(id) VALUES (@p0)", user);
        await database.ExecuteAsync("INSERT INTO final_roles(id) VALUES (@p0)", role);
        await database.ExecuteAsync("INSERT INTO final_links(user_id,role_id) VALUES (@p0,@p1)", user, role);
        var source = end == LinkForeignKeyEnd.Source;
        var unannotatedTable = source ? "final_roles" : "final_users";
        await database.ExecuteAsync($"DELETE FROM {unannotatedTable} WHERE id=@p0", source ? role : user);
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM final_links")));
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(
            "INSERT INTO final_links(user_id,role_id) VALUES (@p0,@p1)", Guid.NewGuid(), Guid.NewGuid()));
        await database.ExecuteAsync("INSERT INTO final_links(user_id,role_id) VALUES (@p0,@p1)",
            source ? user : Guid.NewGuid(), source ? Guid.NewGuid() : role);
        Assert.Equal(2L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM final_links")));
        var annotatedTable = source ? "final_users" : "final_roles";
        await database.ExecuteAsync($"DELETE FROM {annotatedTable} WHERE id=@p0", source ? user : role);
        Assert.Equal(0L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM final_links")));
    }
}
