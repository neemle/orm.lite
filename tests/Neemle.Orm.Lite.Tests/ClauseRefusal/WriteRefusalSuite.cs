using System.Linq.Expressions;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public abstract class WriteRefusalSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Context => fixture.Context;

    private static async Task RunAsync<T>(Query<T> query, string operation, Expression<Func<T, object?>> column)
        where T : class
    {
        switch (operation)
        {
            case "UpdateAsync": await query.Set(column, 99).UpdateAsync(); break;
            case "DeleteAsync": await query.DeleteAsync(); break;
            case "UpdateReturningAsync": await query.Set(column, 99).UpdateReturningAsync([column]); break;
            case "DeleteReturningAsync": await query.DeleteReturningAsync([column]); break;
            default: await query.PurgeAsync(); break;
        }
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] a refused write on a soft-delete entity runs no statement: every row, star and deleted flag is " +
            "unchanged")]
    [MemberData(nameof(ClauseMatrix.ExecutedWrites), MemberType = typeof(ClauseMatrix))]
    public async Task A_refused_write_changes_no_soft_delete_row(string operation, string clause)
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        var before = await ClauseSeed.NotesSnapshotAsync(Context, tenant);
        var query = ClauseTargets.Note.Apply(ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant), clause);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(query, operation, n => n.Stars));

        ClauseNames.Verify(refusal.Message, operation, clause);
        Assert.Equal(before, await ClauseSeed.NotesSnapshotAsync(Context, tenant));
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] a refused write on an entity without soft delete runs no statement: every row is unchanged")]
    [MemberData(nameof(ClauseMatrix.ExecutedWrites), MemberType = typeof(ClauseMatrix))]
    public async Task A_refused_write_changes_no_hard_delete_row(string operation, string clause)
    {
        fixture.RequireAvailable();
        var ids = await ClauseSeed.CountersAsync(Context);
        var before = await ClauseSeed.CountersSnapshotAsync(Context, ids);
        var query = ClauseTargets.Counter.Apply(ClauseSeed.Counters(Context).Where(c => ids.Contains(c.Id)), clause);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(query, operation, c => c.N));

        ClauseNames.Verify(refusal.Message, operation, clause);
        Assert.Equal(before, await ClauseSeed.CountersSnapshotAsync(Context, ids));
    }

    [SkippableFact(
        DisplayName =
            "[SEC] a refused purge leaves an already soft-deleted row where it was, as well as every live one")]
    public async Task A_refused_purge_keeps_the_soft_deleted_row()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        await ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant && n.Title == "t1").DeleteAsync();
        var before = await ClauseSeed.NotesSnapshotAsync(Context, tenant);
        var query = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant).Take(2);

        await Assert.ThrowsAsync<InvalidOperationException>(() => query.PurgeAsync());

        Assert.Equal(before, await ClauseSeed.NotesSnapshotAsync(Context, tenant));
        Assert.Contains("t1:1:False", before, StringComparison.Ordinal);
    }

    [SkippableFact(
        DisplayName =
            "[SEC] the reported reproduction, Where OrderBy Take(100) DeleteAsync over 1000 rows, is refused and all " +
            "1000 rows remain")]
    public async Task The_reported_thousand_row_delete_is_refused()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context, Enumerable.Range(0, 1000));
        var query = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant && n.Stars >= 0);
        query = ClauseTargets.Note.Apply(query, "TakeOrderBy");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.DeleteAsync());

        ClauseNames.Verify(refusal.Message, "DeleteAsync", "TakeOrderBy");
        Assert.Equal(1000L, await ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant).CountAsync());
    }

    [SkippableFact(
        DisplayName =
            "a Where-only DeleteAsync soft-deletes exactly the matching rows and reports their number")]
    public async Task A_where_only_delete_affects_exactly_the_matches()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);

        var affected = await ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant && n.Stars > 3).DeleteAsync();

        Assert.Equal(2, affected);
        Assert.Equal(
            "t1:1:True|t2:2:True|t3:3:True|t4:4:False|t5:5:False",
            await ClauseSeed.NotesSnapshotAsync(Context, tenant));
    }

    [SkippableFact(DisplayName = "a Where-only UpdateAsync changes exactly the matching rows and reports their number")]
    public async Task A_where_only_update_affects_exactly_the_matches()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);

        var query = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant && n.Stars < 3);

        var affected = await query.Set(n => n.Stars, 30).UpdateAsync();

        Assert.Equal(2, affected);
        Assert.Equal(
            "t1:30:True|t2:30:True|t3:3:True|t4:4:True|t5:5:True",
            await ClauseSeed.NotesSnapshotAsync(Context, tenant));
    }
}
