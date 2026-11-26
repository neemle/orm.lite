using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

internal static class SdetExistingBaseline
{
    internal static async Task VerifyAsync(ForeignKeyDatabase database)
    {
        var model = ForeignKeyEnforcement.Model(ReferentialAction.Cascade);
        var oldChild = EntityDescriptor.Of("OldChild", "a_children", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false);
        await database.Context.MigrateAsync(OrmSchema.Of(model.Token, oldChild,
            model.Descriptor("Parent")).Contributions());
        var child = Guid.NewGuid();
        var absent = Guid.NewGuid();
        await database.ExecuteAsync("INSERT INTO a_children(id,parent_id) VALUES (@p0,@p1)", child, absent);
        var history = await database.ScalarAsync(
            "SELECT string_agg(version, ',' ORDER BY version) FROM schema_migrations");
        await database.Context.MigrateAsync(model.Contributions);
        Assert.Empty(await database.ForeignKeysAsync("a_children"));
        Assert.Equal(history, await database.ScalarAsync(
            "SELECT string_agg(version, ',' ORDER BY version) FROM schema_migrations"));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM a_children WHERE id=@p0 AND parent_id=@p1", child, absent)));
        Assert.Equal(1, await database.ExecuteAsync(
            "INSERT INTO a_children(id,parent_id) VALUES (@p0,@p1)", Guid.NewGuid(), absent));
    }
}
