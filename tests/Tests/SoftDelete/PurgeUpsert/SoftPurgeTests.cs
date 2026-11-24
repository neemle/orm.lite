using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Neemle.OrmLite.Tests.SoftDelete.Writes;
using Neemle.OrmLite;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.PurgeUpsert;

public sealed class SoftPurgeTests : SoftWriteProbe
{
    private const string UnfilteredPurgeRefusal = "PurgeAsync requires a Where (refusing an unfiltered DELETE).";

    private async Task<long> PresentAsync(Guid id) =>
        (long)(await ScalarAsync("SELECT COUNT(*) FROM soft_roots WHERE id = $id", ("$id", id)))!;

    [Fact(DisplayName = "AC7: Query.RenderPurge of a soft entity is a physical DELETE with no live predicate, so it reaches deleted rows")]
    public void Purge_renders_a_physical_delete_over_every_row()
    {
        var sql = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderPurge();

        Assert.Equal("DELETE FROM \"soft_roots\" WHERE \"rank\" = @p0", sql);
    }

    [Fact(DisplayName = "AC7: Query.RenderPurge with no Where is refused")]
    public void Purge_with_no_where_is_refused_at_render()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => SoftRootRender.Roots().RenderPurge());

        Assert.Equal(UnfilteredPurgeRefusal, thrown.Message);
    }

    [Fact(DisplayName = "AC7: PurgeAsync removes the matching live row and the matching soft-deleted row, and leaves the rest")]
    public async Task Purge_removes_live_and_deleted_matches_only()
    {
        var before = await RowCountAsync();

        var removed = await Db.Roots().Where(r => r.Name == SoftDeleteDb.DeletedName).PurgeAsync();

        Assert.Equal(1, removed);
        Assert.Equal(0, await PresentAsync(SoftDeleteDb.DeletedRootId));
        Assert.Equal(before - 1, await RowCountAsync());
        Assert.Equal(1, await PresentAsync(SoftDeleteDb.LiveRootId));

        Assert.Equal(1, await Db.Roots().Where(r => r.Name == SoftDeleteDb.LiveName).PurgeAsync());
        Assert.Equal(0, await RowCountAsync());
    }

    [Fact(DisplayName = "AC7: PurgeAsync with no Where is refused and removes nothing")]
    public async Task Unfiltered_purge_is_refused_and_removes_nothing()
    {
        var before = await RowCountAsync();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Roots().PurgeAsync());

        Assert.Equal(UnfilteredPurgeRefusal, thrown.Message);
        Assert.Equal(before, await RowCountAsync());
    }

    [Fact(DisplayName = "AC7: no delete renderer of a soft entity other than RenderPurge starts with DELETE")]
    public void Only_purge_renders_a_physical_delete_for_a_soft_entity()
    {
        var statements = new Dictionary<string, string>
        {
            [nameof(Query<SoftRoot>.RenderDelete)] = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderDelete(),
            [nameof(Query<SoftRoot>.RenderDeleteReturning)] = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderDeleteReturning([r => r.Id]),
            [nameof(Query<SoftRoot>.RenderDelete) + "+escape"] = SoftRootRender.Roots().IncludingDeleted().Where(r => r.Rank == 10).RenderDelete(),
            [nameof(Query<SoftRoot>.RenderPurge)] = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderPurge(),
        };

        Assert.Equal(
            [nameof(Query<SoftRoot>.RenderPurge)],
            statements.Where(s => s.Value.StartsWith("DELETE", StringComparison.Ordinal)).Select(s => s.Key).ToArray());
    }
}
