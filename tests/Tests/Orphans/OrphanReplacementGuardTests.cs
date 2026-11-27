using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

// Concurrency, visibility and cancellation of the public engine over real SQLite and PostgreSQL tables.
[Collection("Graph")]
public sealed class OrphanReplacementGuardTests
{
    [Theory(DisplayName = "[UC-007][AC-113-03][SEC-113-01] while a scope is open no writer can add or reparent a child; a later arrival is never removed by omission")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Concurrent_arrival_and_reparent_cannot_expand_or_shift_candidates(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var id in new[] { 11, 12 }) await db.SeedAsync("orep_children", id, 1, "c" + id);
        const string late = "INSERT INTO orep_children(id,parent_id,name) VALUES(@p0,@p1,'late')";
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
        {
            Assert.NotNull(await db.SideWriteAsync(late, Key(14), Key(1)));
            Assert.NotNull(await db.SideWriteAsync("UPDATE orep_children SET parent_id=@p0 WHERE id=@p1", Key(2), Key(12)));
            Assert.Equal(1, (await scope.ApplyAsync(Keep(11))).Nullified);
        }
        Assert.Null(await db.SideWriteAsync(late, Key(14), Key(1)));
        await using (var repeat = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
        {
            Assert.Equal([Key(11), Key(14)], repeat.Observation.OriginalKeys.OrderBy(k => k));
            Assert.Equal(0, (await repeat.ApplyAsync(Keep(11, 14))).Nullified);
        }
        Assert.Equal([(Key(11), Key(1), "c11"), (Key(12), null, "c12"), (Key(14), Key(1), "late")], await db.ChildrenAsync());
    }

    [Theory(DisplayName = "[UC-007][AC-113-05][AC-113-06] soft-deleted members are invisible to the scope and stay untouched; SoftDelete keeps the row and owner")]
    [InlineData("sqlite", OrphanMode.Nullify)] [InlineData("postgres", OrphanMode.Nullify)]
    [InlineData("sqlite", OrphanMode.SoftDelete)] [InlineData("postgres", OrphanMode.SoftDelete)]
    public async Task Hidden_soft_rows_are_never_candidates(string provider, OrphanMode mode)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var id in new[] { 41, 42, 43 }) await db.SeedAsync("orep_soft", id, 1, "s" + id);
        await db.ExecuteAsync("UPDATE orep_soft SET deleted_at=@p0 WHERE id=@p1", DateTime.UtcNow, Key(42));
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, SoftChildren, Key(1), Options(mode)))
        {
            Assert.Equal([Key(41), Key(43)], scope.Observation.OriginalKeys.OrderBy(k => k));
            var result = await scope.ApplyAsync(new([Key(41)], OrphanPresentWrites<ReplaceSoftChild>.None));
            Assert.Equal(mode == OrphanMode.Nullify ? (1, 0) : (0, 1), (result.Nullified, result.SoftDeleted));
        }
        Assert.Equal(3, (await db.ChildrenAsync("orep_soft")).Count);
        Assert.Equal(1L, await db.CountAsync("SELECT COUNT(*) FROM orep_soft WHERE id=@p0 AND parent_id=@p1 AND deleted_at IS NOT NULL", Key(42), Key(1)));
        Assert.Equal(1L, await db.CountAsync("SELECT COUNT(*) FROM orep_soft WHERE id=@p0 AND parent_id=@p1 AND deleted_at IS NULL", Key(41), Key(1)));
        var removed = mode == OrphanMode.Nullify
            ? "SELECT COUNT(*) FROM orep_soft WHERE id=@p0 AND parent_id IS NULL AND deleted_at IS NULL"
            : "SELECT COUNT(*) FROM orep_soft WHERE id=@p0 AND parent_id=@p1 AND deleted_at IS NOT NULL";
        Assert.Equal(1L, await db.CountAsync(removed, Key(43), Key(1)));
    }

    [Theory(DisplayName = "[UC-007][AC-113-07][AC-113-09] cancellation after a successful orphan action rolls the whole replacement back and keeps the caller token")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Cancellation_after_first_action_rolls_back(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var id in new[] { 11, 12 }) await db.SeedAsync("orep_children", id, 1, "c" + id);
        var before = await db.ChildrenAsync();
        using var cancel = new CancellationTokenSource();
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
        {
            db.ClearEvents();
            db.OnWrite = s => { if (s.RowsAffected == 1) cancel.Cancel(); };
            var failure = await Assert.ThrowsAsync<OrphanCanceledException>(() => scope.ApplyAsync(Keep(), cancel.Token));
            db.OnWrite = null;
            Assert.Equal((OrphanOutcome.RolledBack, cancel.Token), (failure.Outcome, failure.CancellationToken));
        }
        Assert.Equal(1, db.Statements.Count(s => s.Kind == StatementKind.Write && s.RowsAffected == 1));
        Assert.Equal([TransactionPhase.Rollback], db.Transactions.Select(t => t.Phase));
        Assert.Equal(before, await db.ChildrenAsync());
    }
}
