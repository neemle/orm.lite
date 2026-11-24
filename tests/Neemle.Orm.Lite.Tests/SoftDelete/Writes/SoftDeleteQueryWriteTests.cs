using Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;
using Neemle.Orm.Lite;
using Xunit;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Writes;

public sealed class SoftDeleteQueryWriteTests : SoftWriteProbe
{
    private const string UnfilteredDeleteRefusal = "DeleteAsync requires a Where (refusing an unfiltered DELETE).";
    private const string UnfilteredUpdateRefusal = "UpdateAsync requires a Where (refusing an unfiltered UPDATE).";
    private const string UnfilteredDeleteReturningRefusal = "DeleteReturningAsync requires a Where (refusing an unfiltered DELETE).";
    private const string UnfilteredUpdateReturningRefusal = "UpdateReturningAsync requires a Where (refusing an unfiltered UPDATE).";

    [Fact(DisplayName = "AC3: Query.DeleteAsync on a soft-delete entity leaves the row present with deleted_at set and returns the count marked")]
    public async Task Query_delete_marks_the_live_row_and_does_not_remove_it()
    {
        Assert.Null(await DeletedAtOfAsync(SoftDeleteDb.LiveRootId));
        var before = await RowCountAsync();

        var marked = await Db.Roots().Where(r => r.Name == SoftDeleteDb.LiveName).DeleteAsync();

        Assert.Equal(1, marked);
        Assert.Equal(before, await RowCountAsync());
        Assert.NotNull(await DeletedAtOfAsync(SoftDeleteDb.LiveRootId));
    }

    [Fact(DisplayName = "AC3: Query.DeleteAsync renders one UPDATE that sets the soft column from the database clock over live rows, and no DELETE")]
    public void Query_delete_renders_an_update_over_live_rows()
    {
        var sql = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderDelete();

        Assert.Equal("UPDATE \"soft_roots\" SET \"deleted_at\" = now() WHERE \"deleted_at\" IS NULL AND \"rank\" = @p0", sql);
    }

    [Fact(DisplayName = "AC3: Query.DeleteReturningAsync renders the same UPDATE over live rows with a RETURNING clause, and no DELETE")]
    public void Query_delete_returning_renders_an_update_over_live_rows()
    {
        var sql = SoftRootRender.Roots().Where(r => r.Rank == 10).RenderDeleteReturning([r => r.Id]);

        Assert.Equal("UPDATE \"soft_roots\" SET \"deleted_at\" = now() WHERE \"deleted_at\" IS NULL AND \"rank\" = @p0 RETURNING \"id\"", sql);
    }

    [Fact(DisplayName = "AC3 control: a non-soft entity's Query.DeleteAsync still renders a physical DELETE")]
    public void A_non_soft_entity_still_renders_a_physical_delete()
    {
        var sql = new Query<SoftReferrer>(
            TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance),
            SoftReferrer.Map)
            .Where(r => r.Name == "x").RenderDelete();

        Assert.Equal("DELETE FROM \"soft_referrers\" WHERE \"name\" = @p0", sql);
    }

    [Fact(DisplayName = "AC4: deleting an already-deleted row changes nothing, leaves deleted_at as it was and counts 0")]
    public async Task Query_delete_of_a_deleted_row_changes_nothing()
    {
        var before = await DeletedAtOfAsync(SoftDeleteDb.DeletedRootId);

        var marked = await Db.Roots().Where(r => r.Name == SoftDeleteDb.DeletedName).DeleteAsync();

        Assert.Equal(0, marked);
        Assert.NotNull(before);
        Assert.Equal(before, await DeletedAtOfAsync(SoftDeleteDb.DeletedRootId));
    }

    [Fact(DisplayName = "AC4: the escape does not let a delete overwrite deleted_at on a deleted row")]
    public async Task Query_delete_with_the_escape_does_not_overwrite_a_deleted_row()
    {
        var before = await DeletedAtOfAsync(SoftDeleteDb.DeletedRootId);

        var marked = await Db.Roots().IncludingDeleted().Where(r => r.Name == SoftDeleteDb.DeletedName).DeleteAsync();

        Assert.Equal(0, marked);
        Assert.Equal(before, await DeletedAtOfAsync(SoftDeleteDb.DeletedRootId));
    }

    [Fact(DisplayName = "AC4: Query.UpdateAsync without the escape does not modify a soft-deleted row and counts 0")]
    public async Task Query_update_does_not_reach_a_deleted_row()
    {
        var updated = await Db.Roots().Where(r => r.Name == SoftDeleteDb.DeletedName).Set(r => r.Rank, 7).UpdateAsync();

        Assert.Equal(0, updated);
        Assert.Equal(SoftDeleteDb.DeletedRank, await RankOfAsync(SoftDeleteDb.DeletedRootId));
    }

    [Fact(DisplayName = "AC4: Query.UpdateAsync reaches a live row")]
    public async Task Query_update_reaches_a_live_row()
    {
        var updated = await Db.Roots().Where(r => r.Name == SoftDeleteDb.LiveName).Set(r => r.Rank, 7).UpdateAsync();

        Assert.Equal(1, updated);
        Assert.Equal(7, await RankOfAsync(SoftDeleteDb.LiveRootId));
    }

    [Fact(DisplayName = "AC4 control: Query.UpdateAsync after IncludingDeleted() reaches the soft-deleted row, so the guard under test is the live predicate")]
    public async Task Query_update_with_the_escape_reaches_the_deleted_row()
    {
        var updated = await Db.Roots().IncludingDeleted().Where(r => r.Name == SoftDeleteDb.DeletedName).Set(r => r.Rank, 7).UpdateAsync();

        Assert.Equal(1, updated);
        Assert.Equal(7, await RankOfAsync(SoftDeleteDb.DeletedRootId));
    }

    [Fact(DisplayName = "AC4: Query.UpdateReturningAsync renders the live predicate without the escape and not after it")]
    public void Query_update_returning_renders_the_live_predicate_only_without_the_escape()
    {
        var live = SoftRootRender.Roots().Where(r => r.Rank == 10).Set(r => r.Name, "n").RenderUpdateReturning([r => r.Id]);
        var escaped = SoftRootRender.Roots().IncludingDeleted().Where(r => r.Rank == 10).Set(r => r.Name, "n").RenderUpdateReturning([r => r.Id]);

        Assert.Contains("\"deleted_at\" IS NULL", live);
        Assert.DoesNotContain("\"deleted_at\" IS NULL", escaped);
    }

    [Fact(DisplayName = "AC5: Query.DeleteAsync with no user Where is refused with the existing message on a soft-delete entity and marks nothing")]
    public async Task Unfiltered_delete_is_refused_and_marks_nothing()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Roots().DeleteAsync());

        Assert.Equal(UnfilteredDeleteRefusal, thrown.Message);
        Assert.Null(await DeletedAtOfAsync(SoftDeleteDb.LiveRootId));
    }

    [Fact(DisplayName = "AC5: Query.UpdateAsync with no user Where is refused with the existing message on a soft-delete entity and changes nothing")]
    public async Task Unfiltered_update_is_refused_and_changes_nothing()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Roots().Set(r => r.Rank, 7).UpdateAsync());

        Assert.Equal(UnfilteredUpdateRefusal, thrown.Message);
        Assert.Equal(SoftDeleteDb.LiveRank, await RankOfAsync(SoftDeleteDb.LiveRootId));
    }

    [Fact(DisplayName = "AC5: both RETURNING forms with no user Where are refused with the existing messages on a soft-delete entity")]
    public void Unfiltered_returning_forms_are_refused()
    {
        var delete = Assert.Throws<InvalidOperationException>(() => SoftRootRender.Roots().RenderDeleteReturning([r => r.Id]));
        var update = Assert.Throws<InvalidOperationException>(() => SoftRootRender.Roots().Set(r => r.Rank, 7).RenderUpdateReturning([r => r.Id]));

        Assert.Equal(UnfilteredDeleteReturningRefusal, delete.Message);
        Assert.Equal(UnfilteredUpdateReturningRefusal, update.Message);
    }

    [Fact(DisplayName = "AC5: the unfiltered refusal is the same after IncludingDeleted()")]
    public void Unfiltered_delete_is_refused_after_the_escape()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => SoftRootRender.Roots().IncludingDeleted().RenderDelete());

        Assert.Equal(UnfilteredDeleteRefusal, thrown.Message);
    }
}
