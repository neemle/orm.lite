using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class SqliteForeignKeyTests
{
    [Fact(DisplayName = "[UC-003] enabled SQLite catalogs and enforces all four fresh FK delete policies and reruns")]
    public async Task Enabled_actions_have_setting_and_real_dml_witnesses()
    {
        foreach (var action in Enum.GetValues<ReferentialAction>())
        {
            await using var database = await ForeignKeyDatabase.SqliteAsync();
            Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
            await ForeignKeyEnforcement.VerifyAsync(database, action);
        }
    }

    [Fact(DisplayName = "[SEC][UC-003] SQLite honors explicit disabled enforcement despite catalog declarations")]
    public async Task Disabled_configuration_is_preserved_and_is_not_enforcement()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync(false);
        await database.Context.MigrateAsync(ForeignKeyEnforcement.Model(ReferentialAction.Cascade).Contributions);
        Assert.Equal(0L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
        Assert.Single(await database.ForeignKeysAsync("a_children"));
        Assert.Equal(1, await database.ExecuteAsync("INSERT INTO a_children (id,parent_id) VALUES (@p0,@p1)",
            Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact(DisplayName = "[SEC][UC-003] quoted SQLite constraint names remain data and cannot execute statements")]
    public async Task Hostile_quoted_name_keeps_exact_catalog_identity()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        await ForeignKeyEnforcement.QuotedNameAsync(database);
    }

    [Fact(DisplayName = "[UC-003] existing baseline history and orphan data are never implicitly retrofitted")]
    public async Task Model_opt_in_does_not_reapply_an_already_recorded_baseline()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        var model = ForeignKeyEnforcement.Model(ReferentialAction.Restrict);
        var owner = model.Descriptor("Child");
        var oldChild = EntityDescriptor.Of(owner.ClrType, owner.Table, true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false);
        var oldSchema = OrmSchema.Of(model.Token, oldChild, model.Descriptor("Parent"));
        await database.Context.MigrateAsync(oldSchema.Contributions());
        await database.ExecuteAsync("INSERT INTO a_children (id,parent_id) VALUES (@p0,@p1)",
            Guid.NewGuid(), Guid.NewGuid());
        var history = await database.ScalarAsync("SELECT count(*) FROM schema_migrations");
        await database.Context.MigrateAsync(model.Contributions);
        Assert.Empty(await database.ForeignKeysAsync("a_children"));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("SELECT count(*) FROM a_children")));
        Assert.Equal(history, await database.ScalarAsync("SELECT count(*) FROM schema_migrations"));
    }
    [Fact(DisplayName = "[UC-003] generated pure links enforce independent Restrict and Cascade endpoints")]
    public async Task Pure_link_has_real_endpoint_integrity_and_actions()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        await ForeignKeyLinks.VerifyAsync(database);
    }
}
