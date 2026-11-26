using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

internal static class SdetUpdateWitness
{
    internal static async Task VerifyAsync(ForeignKeyDatabase database)
    {
        var model = ForeignKeyEnforcement.Model(ReferentialAction.SetNull);
        await database.Context.MigrateAsync(model.Contributions);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var child = Guid.NewGuid();
        await database.ExecuteAsync("INSERT INTO z_parents(id) VALUES (@p0),(@p1)", first, second);
        await database.ExecuteAsync("INSERT INTO a_children(id,parent_id) VALUES (@p0,@p1)", child, first);
        Assert.Equal(1, await database.ExecuteAsync(
            "UPDATE a_children SET parent_id=@p0 WHERE id=@p1", second, child));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM a_children WHERE id=@p0 AND parent_id=@p1", child, second)));
        Assert.Equal(1, await database.ExecuteAsync(
            "UPDATE a_children SET parent_id=NULL WHERE id=@p0", child));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM a_children WHERE id=@p0 AND parent_id IS NULL", child)));
        Assert.Equal(1, await database.ExecuteAsync(
            "UPDATE a_children SET parent_id=@p0 WHERE id=@p1", first, child));
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync(
            "SELECT count(*) FROM a_children WHERE id=@p0 AND parent_id=@p1", child, first)));
    }
}
