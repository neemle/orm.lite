using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

// ORML-63: a row lock on a query is taken by the reads that return rows and refused by every other terminal. The
// refusal comes before any statement, so a refused write changes no row.
public abstract class LockRefusalSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Context => fixture.Context;

    private async Task<(Guid Tenant, Query<BehaveNote> Query)> LockedAsync(string rowLock)
    {
        var tenant = await ClauseSeed.NotesAsync(Context);
        var query = ClauseSeed.Notes(Context).Where(n => n.TenantId == tenant);
        return (tenant, ClauseTargets.Note.Apply(query, rowLock));
    }

    private static async Task RunReadAsync(Query<BehaveNote> query, string operation)
    {
        switch (operation)
        {
            case "CountAsync": await query.CountAsync(); break;
            case "AnyAsync": await query.AnyAsync(); break;
            case "SumAsync": await query.SumAsync(n => n.Stars); break;
            case "ScalarAsync": await query.ScalarAsync<int>(n => n.Stars); break;
            case "GroupBy": await query.GroupBy(n => n.Stars).ToListAsync(); break;
            default: await query.AggregateCount().AggregatesAsync(); break;
        }
    }

    private static async Task RunWriteAsync(Query<BehaveNote> query, string operation)
    {
        switch (operation)
        {
            case "UpdateAsync": await query.Set(n => n.Stars, 99).UpdateAsync(); break;
            case "DeleteAsync": await query.DeleteAsync(); break;
            case "UpdateReturningAsync": await query.Set(n => n.Stars, 99).UpdateReturningAsync([n => n.Stars]); break;
            case "DeleteReturningAsync": await query.DeleteReturningAsync([n => n.Stars]); break;
            default: await query.PurgeAsync(); break;
        }
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] a count, an any, a sum, the aggregate row, a scalar and a grouping over a locked query are " +
            "refused instead of dropping the lock")]
    [MemberData(nameof(LockMatrix.ExecutedTerminals), MemberType = typeof(LockMatrix))]
    public async Task A_locked_read_terminal_is_refused(string operation, string rowLock)
    {
        fixture.RequireAvailable();
        var (_, query) = await LockedAsync(rowLock);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunReadAsync(query, operation));

        LockNames.Verify(refusal.Message, operation);
    }

    [SkippableTheory(
        DisplayName =
            "[SEC] a write over a locked query is refused and runs no statement: every row, star and deleted flag " +
            "is unchanged")]
    [MemberData(nameof(LockMatrix.ExecutedWrites), MemberType = typeof(LockMatrix))]
    public async Task A_locked_write_changes_no_row(string operation, string rowLock)
    {
        fixture.RequireAvailable();
        var (tenant, query) = await LockedAsync(rowLock);
        var before = await ClauseSeed.NotesSnapshotAsync(Context, tenant);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunWriteAsync(query, operation));

        LockNames.Verify(refusal.Message, operation);
        Assert.Equal(before, await ClauseSeed.NotesSnapshotAsync(Context, tenant));
    }

    [SkippableFact(
        DisplayName =
            "the reads that return rows still take the lock inside a unit of work, and the lock-free totals of the " +
            "same rows still answer")]
    public async Task The_reads_that_return_rows_keep_the_lock()
    {
        fixture.RequireAvailable();
        var tenant = await ClauseSeed.NotesAsync(Context);
        await using var work = await UnitOfWork.BeginAsync(Context);
        Query<BehaveNote> Notes() => ClauseSeed.Notes(Context).On(work).Where(n => n.TenantId == tenant);

        var listed = await Notes().ForUpdate().ToListAsync();
        var first = await Notes().ForNoKeyUpdate().OrderBy(n => n.Stars).FirstOrDefaultAsync();
        var projected = await Notes().ForUpdate().ProjectAsync([n => n.Stars], r => r.GetInt32(0));

        Assert.Equal(5, listed.Count);
        Assert.Equal("t1", first?.Title);
        Assert.Equal(5, projected.Count);
        Assert.Equal(5L, await Notes().CountAsync());
        Assert.Equal(15L, await Notes().SumAsync(n => n.Stars));
        Assert.Equal(1, await Notes().OrderBy(n => n.Stars).ScalarAsync<int>(n => n.Stars));
    }
}
