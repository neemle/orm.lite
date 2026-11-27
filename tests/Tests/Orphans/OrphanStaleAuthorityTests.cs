using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

// The write statements are the last line of defence: each binds the exact child key AND the exact parent, and a write
// that does not change exactly one row ends the replacement. The rows change inside the scope after the guard has
// verified (StaleGuardProvider), so nothing but the write statements can notice.
[Collection("Graph")]
public sealed class OrphanStaleAuthorityTests
{
    private const string Reparent = "UPDATE orep_children SET parent_id=@p0 WHERE id=@p1";
    private const string Vanish = "DELETE FROM orep_children WHERE id=@p0";

    [Theory(DisplayName = "[UC-007][SEC-113-01][AC-113-07] an orphan removal binds the exact parent: a candidate moved to another parent after the authority is neither nullified nor deleted, and everything rolls back")]
    [InlineData("sqlite", OrphanMode.Nullify)] [InlineData("postgres", OrphanMode.Nullify)]
    [InlineData("sqlite", OrphanMode.Delete)] [InlineData("postgres", OrphanMode.Delete)]
    public async Task Candidate_write_binds_the_exact_parent(string provider, OrphanMode mode)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var (id, parent) in new[] { (11, 1), (12, 1), (21, 2) })
            await db.SeedAsync("orep_children", id, parent, "c" + id);
        var before = await db.ChildrenAsync();
        await using var context = await db.StaleContextAsync(Reparent, Key(2), Key(12));
        db.ClearEvents();
        await using (var scope = await OrphanCollections.BeginAsync(context, Children, Key(1), Options(mode)))
        {
            Assert.Equal([Key(11), Key(12)], scope.Observation.OriginalKeys.OrderBy(k => k));
            await RefusedAsync(() => scope.ApplyAsync(Keep(11)), OrphanError.StaleAuthority);
        }
        Assert.Equal(new int?[] { 0 }, Writes(db));
        OrphanReplacementProviderTests.OneOwnedTransaction(db, TransactionPhase.Rollback);
        Assert.Equal(before, await db.ChildrenAsync());
    }

    [Theory(DisplayName = "[UC-007][SEC-113-01][AC-113-07] a present update binds the exact parent: a desired child moved to another parent after the authority is not pulled back, and everything rolls back")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Present_update_binds_the_exact_parent(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var (id, parent) in new[] { (11, 1), (12, 1), (21, 2) })
            await db.SeedAsync("orep_children", id, parent, "c" + id);
        var before = await db.ChildrenAsync();
        await using var context = await db.StaleContextAsync(Reparent, Key(2), Key(11));
        var update = new ReplaceChild { Id = Key(11), ParentId = Key(1), Name = "changed" };
        db.ClearEvents();
        await using (var scope = await OrphanCollections.BeginAsync(context, Children, Key(1), Options(OrphanMode.Delete)))
            await RefusedAsync(() => scope.ApplyAsync(new([Key(11), Key(12)],
                OrphanPresentWrites<ReplaceChild>.None.Update(update))), OrphanError.MissingExisting);
        Assert.Equal(new int?[] { 0 }, Writes(db));
        OrphanReplacementProviderTests.OneOwnedTransaction(db, TransactionPhase.Rollback);
        Assert.Equal(before, await db.ChildrenAsync());
    }

    [Theory(DisplayName = "[UC-007][AC-113-03][AC-113-07] a write that changes zero rows is never accepted: a candidate gone after the authority refuses with StaleAuthority and the earlier removal rolls back")]
    [InlineData("sqlite", OrphanMode.Nullify)] [InlineData("postgres", OrphanMode.Nullify)]
    [InlineData("sqlite", OrphanMode.Delete)] [InlineData("postgres", OrphanMode.Delete)]
    public async Task Vanished_candidate_is_stale_authority(string provider, OrphanMode mode)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var id in new[] { 11, 12, 13 }) await db.SeedAsync("orep_children", id, 1, "c" + id);
        var before = await db.ChildrenAsync();
        await using var context = await db.StaleContextAsync(Vanish, Key(13));
        db.ClearEvents();
        await using (var scope = await OrphanCollections.BeginAsync(context, Children, Key(1), Options(mode)))
        {
            Assert.Equal([Key(11), Key(12), Key(13)], scope.Observation.OriginalKeys.OrderBy(k => k));
            await RefusedAsync(() => scope.ApplyAsync(Keep(11)), OrphanError.StaleAuthority);
        }
        Assert.Equal(new int?[] { 1, 0 }, Writes(db));
        OrphanReplacementProviderTests.OneOwnedTransaction(db, TransactionPhase.Rollback);
        Assert.Equal(before, await db.ChildrenAsync());
    }

    private static async Task RefusedAsync(Func<Task<OrphanResult>> apply, OrphanError error)
    {
        var failure = await Assert.ThrowsAsync<OrphanException>(apply);
        Assert.Equal((error, OrphanOutcome.RolledBack), (failure.Error, failure.Outcome));
        Assert.Null(failure.CommittedResult);
    }

    private static int?[] Writes(ReplaceDatabase db) =>
        db.Statements.Where(s => s.Kind == StatementKind.Write).Select(s => s.RowsAffected).ToArray();
}
