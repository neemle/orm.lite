using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

// The public Begin/Apply engine over real SQLite and PostgreSQL tables: exact candidates, scope and repetition.
[Collection("Graph")]
public sealed class OrphanReplacementProviderTests
{
    [Theory(DisplayName = "[UC-007][AC-113-02][AC-113-04][AC-113-08][AC-113-09] a subset replacement nullifies only the removed original, in one owned transaction, and a repeat broadens nothing")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Subset_replacement_nullifies_only_removed_children(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var (id, parent) in new[] { (11, 1), (12, 1), (13, 1), (21, 2) })
            await db.SeedAsync("orep_children", id, parent, "c" + id);
        db.ClearEvents();
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
        {
            Assert.True(scope.Observation.IsAuthoritative);
            Assert.Equal([Key(11), Key(12), Key(13)], scope.Observation.OriginalKeys.OrderBy(k => k));
            var result = await scope.ApplyAsync(Keep(11, 13));
            Assert.Equal((OrphanOutcome.Committed, false, 1, 0, 0), (result.Outcome, result.Disabled, result.Nullified, result.Deleted, result.SoftDeleted));
            var again = await Assert.ThrowsAsync<OrphanException>(() => scope.ApplyAsync(Keep()));
            Assert.Equal((OrphanError.SnapshotConsumed, OrphanOutcome.NotStarted), (again.Error, again.Outcome));
        }
        Assert.Equal([(Key(11), Key(1), "c11"), (Key(12), null, "c12"), (Key(13), Key(1), "c13"), (Key(21), Key(2), "c21")],
            await db.ChildrenAsync());
        OneOwnedTransaction(db, TransactionPhase.Commit);
        var write = Assert.Single(db.Statements, s => s.Kind == StatementKind.Write);
        Assert.Equal(1, write.RowsAffected);
        Assert.DoesNotContain(Key(12).ToString(), write.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Key(1).ToString(), write.Text, StringComparison.OrdinalIgnoreCase);
        await using (var repeat = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
            Assert.Equal(0, (await repeat.ApplyAsync(Keep(11, 13))).Nullified);
        Assert.Equal(Key(1), (await db.ChildrenAsync())[2].ParentId);
    }

    [Theory(DisplayName = "[UC-007][AC-113-02][AC-113-05][AC-113-06] an explicit empty replacement physically deletes all and only the observed children of that parent")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Empty_replacement_deletes_only_observed_children(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var (id, parent) in new[] { (11, 1), (12, 1), (21, 2) })
            await db.SeedAsync("orep_children", id, parent, "c" + id);
        await db.SeedAsync("orep_children", 31, null, "unowned");
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Delete)))
            Assert.Equal(2, (await scope.ApplyAsync(Keep())).Deleted);
        Assert.Equal([(Key(21), Key(2), "c21"), (Key(31), null, "unowned")], await db.ChildrenAsync());
    }

    [Theory(DisplayName = "[UC-007][AC-113-03][AC-113-07][SEC-113-02] a provider failure after a successful orphan delete rolls back the present update and every removal")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Failure_after_successful_orphan_action_rolls_back_everything(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        foreach (var id in new[] { 11, 12, 13 }) await db.SeedAsync("orep_children", id, 1, "c" + id);
        await db.ExecuteAsync("INSERT INTO orep_pins(id,child_id) VALUES(@p0,@p1)", Key(91), Key(13));
        var before = await db.ChildrenAsync();
        var update = new ReplaceChild { Id = Key(11), ParentId = Key(1), Name = "changed" };
        db.ClearEvents();
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.Delete)))
        {
            var failure = await Assert.ThrowsAsync<OrphanException>(() => scope.ApplyAsync(new([Key(11)],
                OrphanPresentWrites<ReplaceChild>.None.Update(update))));
            Assert.Equal((OrphanError.ProviderFailure, OrphanOutcome.RolledBack), (failure.Error, failure.Outcome));
            Assert.Null(failure.CommittedResult);
            Assert.DoesNotContain("changed", failure.ToString());
        }
        var writes = db.Statements.Where(s => s.Kind == StatementKind.Write).ToArray();
        Assert.Equal(3, writes.Length);
        Assert.All(writes.Take(2), s => Assert.Equal((1, (Exception?)null), (s.RowsAffected ?? -1, s.Error)));
        // SEC-113-01: orphan writes are redacted like graph-save statements, so the events carry no SQL text or
        // values; the update, the successful delete and the failing delete are told apart by order and outcome.
        Assert.All(writes, s => { Assert.Equal("graph-save", s.Text); Assert.Null(s.Parameters); });
        Assert.NotNull(writes[2].Error);
        OneOwnedTransaction(db, TransactionPhase.Rollback);
        Assert.Equal(before, await db.ChildrenAsync());
        Assert.Equal("changed", update.Name);
    }

    [Theory(DisplayName = "[UC-007][AC-113-04][AC-113-05][SEC-113-02] unsupported modes refuse before any I/O and never fall back to another removal")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Unsupported_modes_refuse_before_io(string provider)
    {
        await using var db = await ReplaceDatabase.OpenAsync(provider);
        await db.ExecuteAsync("INSERT INTO orep_strict(id,parent_id) VALUES(@p0,@p1)", Key(51), Key(1));
        await db.SeedAsync("orep_children", 11, 1, "c11");
        await db.SeedAsync("orep_soft", 41, 1, "s41");
        db.ClearEvents();
        await Refused(() => OrphanCollections.BeginAsync(db.Context, StrictChildren, Key(1), Options(OrphanMode.Nullify)),
            OrphanError.Nullability);
        await Refused(() => OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(OrphanMode.SoftDelete)),
            OrphanError.MissingSoftDelete);
        await Refused(() => OrphanCollections.BeginAsync(db.Context, SoftChildren, Key(1), Options(OrphanMode.Delete)),
            OrphanError.ProtectedRow);
        await Refused(() => OrphanCollections.BeginAsync(db.Context, Children, Key(1),
            new(new(20, 20, 20, 20, 400), OrphanMode.Delete, OrphanTransactionMode.CallerJoined)), OrphanError.UnsupportedTransaction);
        Assert.Empty(db.Statements);
        Assert.Empty(db.Transactions);
        Assert.Equal(Key(1), Assert.Single(await db.ChildrenAsync()).ParentId);
        Assert.Equal(Key(1), Assert.Single(await db.ChildrenAsync("orep_soft")).ParentId);
    }

    private static async Task Refused(Func<Task> action, OrphanError error)
    {
        var failure = await Assert.ThrowsAsync<OrphanException>(action);
        Assert.Equal((error, OrphanOutcome.NotStarted), (failure.Error, failure.Outcome));
    }

    internal static void OneOwnedTransaction(ReplaceDatabase db, TransactionPhase ending)
    {
        var transaction = Assert.Single(db.Statements.Select(s => s.TransactionId).Distinct());
        Assert.NotNull(transaction);
        Assert.Single(db.Statements.Select(s => s.ConnectionId).Distinct());
        Assert.Equal([TransactionPhase.Begin, ending], db.Transactions.Select(t => t.Phase));
        Assert.All(db.Transactions, t => Assert.Equal(transaction, t.TransactionId));
    }
}
