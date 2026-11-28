using System.Collections;
using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class GuardNativeProducerTests
{
    [Theory(DisplayName = "[UC-007][AC-113-02][AC-113-08] native guard sees exact supplied transaction and preserves all rows during mode preflight")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Native_observation_and_mode_checks_use_the_supplied_transaction(string provider)
    {
        await using var database = await GuardNativeDatabase.Open(provider);
        var before = await database.Rows();
        await using var unit = await UnitOfWork.BeginAsync(database.Context);
        await using (var insert = GuardNativeDatabase.Command(unit.Connection, unit.Transaction,
            "INSERT INTO guard_children(id,parent_id,name) VALUES(@p0,@p1,'transaction-only')",
            GuardNativeModel.Key(13), GuardNativeModel.Key(1)))
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        var ownRows = await database.Rows(unit.Connection, unit.Transaction);
        Assert.Equal(before, await database.Rows());
        var lease = await database.Guard.AcquireAsync(unit.Connection, unit.Transaction, GuardNativeModel.Request(), default);
        try
        {
            Assert.Equal(new[] { GuardNativeModel.Key(11), GuardNativeModel.Key(12), GuardNativeModel.Key(13) }, lease.OriginalKeys.Cast<Guid>());
            Assert.Throws<NotSupportedException>(() => ((IList)lease.OriginalKeys)[0] = GuardNativeModel.Key(99));
            await lease.VerifyEffectsAsync(OrphanMode.Nullify, default);
            await lease.VerifyEffectsAsync(OrphanMode.Delete, default);
            Assert.Equal(ownRows, await database.Rows(unit.Connection, unit.Transaction));
            Assert.Equal(before, await database.Rows());
        }
        finally { await lease.DisposeAsync(); }
        Assert.Equal(ConnectionState.Open, unit.Connection.State);
        await using (var command = GuardNativeDatabase.Command(unit.Connection, unit.Transaction, "SELECT 1"))
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        var ended = await Assert.ThrowsAsync<OrphanException>(() => lease.VerifyEffectsAsync(OrphanMode.Delete, default));
        Assert.Equal(OrphanError.ScopeEnded, ended.Error);
        await unit.Transaction.RollbackAsync();
        Assert.Equal(before, await database.Rows());
    }

    [Theory(DisplayName = "[UC-007][AC-113-02] a real empty parent collection is a successful complete guard observation")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Empty_live_collection_is_observed_without_blanket_refusal(string provider)
    {
        await using var database = await GuardNativeDatabase.Open(provider);
        var before = await database.Rows();
        await using var unit = await UnitOfWork.BeginAsync(database.Context);
        await using var lease = await database.Guard.AcquireAsync(unit.Connection, unit.Transaction, GuardNativeModel.Request(3), default);
        Assert.Empty(lease.OriginalKeys);
        await lease.VerifyEffectsAsync(OrphanMode.Delete, default);
        Assert.Equal(before, await database.Rows(unit.Connection, unit.Transaction));
        await unit.Transaction.RollbackAsync();
    }

    [Theory(DisplayName = "[UC-007][AC-113-03][AC-113-09] native missing parent and observed overflow return no authoritative prefix")]
    [InlineData("sqlite", false)] [InlineData("postgres", false)]
    [InlineData("sqlite", true)] [InlineData("postgres", true)]
    public async Task Missing_parent_and_observation_overflow_refuse_before_user_mutation(string provider, bool overflow)
    {
        await using var database = await GuardNativeDatabase.Open(provider);
        var before = await database.Rows();
        await using var unit = await UnitOfWork.BeginAsync(database.Context);
        var request = overflow ? GuardNativeModel.Request(observed: 1) : GuardNativeModel.Request(9);
        var failure = await Assert.ThrowsAsync<OrphanException>(() => database.Guard.AcquireAsync(unit.Connection, unit.Transaction, request, default));
        Assert.Equal(overflow ? OrphanError.LimitExceeded : OrphanError.ParentMissing, failure.Error);
        Assert.Equal(OrphanOutcome.NotStarted, failure.Outcome);
        Assert.Equal(ConnectionState.Open, unit.Connection.State);
        Assert.Equal(before, await database.Rows(unit.Connection, unit.Transaction));
        await unit.Transaction.RollbackAsync();
        Assert.Equal(before, await database.Rows());
    }
}
