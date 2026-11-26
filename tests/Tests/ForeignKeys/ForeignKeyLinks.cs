using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class ForeignKeyLinks
{
    internal const string Source = """
        using Neemle.OrmLite.Nodes;
        [Table("users")] public class User
        {
            public Guid Id { get; set; }
            [ManyToMany("user_roles", "user_id", "role_id")]
            [LinkForeignKey(LinkForeignKeyEnd.Source, "fk_user", ReferentialAction.Restrict)]
            [LinkForeignKey(LinkForeignKeyEnd.Target, "fk_role", ReferentialAction.Cascade)]
            public System.Collections.Generic.List<Role> Roles { get; set; } = new();
        }
        [Table("roles")] public class Role
        {
            public Guid Id { get; set; }
            [ManyToMany("user_roles", "role_id", "user_id")]
            [LinkForeignKey(LinkForeignKeyEnd.Source, "fk_role", ReferentialAction.Cascade)]
            [LinkForeignKey(LinkForeignKeyEnd.Target, "fk_user", ReferentialAction.Restrict)]
            public System.Collections.Generic.List<User> Users { get; set; } = new();
        }
        """;

    internal static GeneratedModel Model()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    [Fact(DisplayName = "[UC-003] reversed link navigations normalize to one join with two independent constraints")]
    public void Inverse_link_intent_keeps_one_physical_owner()
    {
        var plan = Model().Schema.Baseline().Cast<CreateTable>().ToArray();
        Assert.Equal(["roles", "users", "user_roles"], plan.Select(t => t.Table));
        Assert.Equal(["fk_role", "fk_user"], plan.Last().Constraints!.Cast<ForeignKeyDef>().Select(k => k.Constraint));
    }

    [Theory(DisplayName = "[UC-003] unsupported generated link shapes refuse the entire baseline")]
    [InlineData("int", "ReferentialAction.Restrict", "UnsupportedLink")]
    [InlineData("Guid", "ReferentialAction.SetNull", "NonNullableSetNull")]
    public void Unsupported_link_has_explicit_diagnostic(string key, string action, string expected)
    {
        var source = Source.Replace("Guid Id", key + " Id")
            .Replace("ReferentialAction.Restrict", action);
        var (diagnostics, _, _) = Run(source);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE035" && d.GetMessage().Contains(expected));
    }

    internal static async Task VerifyAsync(ForeignKeyDatabase database)
    {
        await database.Context.MigrateAsync(Model().Contributions);
        var keys = await database.ForeignKeysAsync("user_roles");
        Assert.Equal(["fk_role", "fk_user"], keys.Select(k => k.Name).Order(StringComparer.Ordinal));
        var user = Guid.NewGuid();
        var role = Guid.NewGuid();
        await database.ExecuteAsync("INSERT INTO users (id) VALUES (@p0)", user);
        await database.ExecuteAsync("INSERT INTO roles (id) VALUES (@p0)", role);
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(
            "INSERT INTO user_roles (user_id,role_id) VALUES (@p0,@p1)", user, Guid.NewGuid()));
        await database.ExecuteAsync("INSERT INTO user_roles (user_id,role_id) VALUES (@p0,@p1)", user, role);
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync("DELETE FROM users WHERE id=@p0", user));
        await database.ExecuteAsync("DELETE FROM roles WHERE id=@p0", role);
        Assert.Equal(0L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM user_roles")));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM users")));
    }
}
