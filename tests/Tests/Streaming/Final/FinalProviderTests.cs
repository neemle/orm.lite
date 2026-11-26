using System.Data;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.Behaviour;
using Neemle.OrmLite.Tests.Streaming.Sdet;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public abstract class FinalProviderTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    [Theory(DisplayName = "[UC-010][SEC02] actual diagnostics preserve parameter-off and enabled secret redaction")]
    [InlineData(false)] [InlineData(true)]
    public async Task Real_parameter_policy_is_preserved(bool enabled)
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        var sink = new SdetStatementSink();
        var options = fixture.NewOptions();
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Information;
        Assert.False(options.Diagnostics.LogParameters);
        if (enabled) options.Diagnostics.LogParameters = true;
        await using var context = await OrmLite.OpenAsync(options);
        var secret = "password=synthetic-streaming-private-value";
        var query = new Query<BehaveNote>(context, BehaveNote.Map)
            .Where(n => n.TenantId == tenant && n.Title != secret).OrderBy(n => n.Stars);
        var rows = await StreamingProviderChecks.CollectAsync(query.StreamAsync());
        Assert.Equal(new[] { 1, 2, 3, 4, 6 }, rows.Select(n => n.Stars));
        var execution = Assert.Single(sink.Statements);
        if (enabled) Assert.Contains(execution.Parameters!, p => p.Value == "[redacted]");
        else Assert.Null(execution.Parameters);
        Assert.DoesNotContain(secret, execution.ToString());
        Assert.Null(execution.Error);
        Assert.Null(execution.RowsAffected);
    }

    [Fact(DisplayName = "[UC-010][SEC02] a real sink configured alone receives no automatic streaming events")]
    public async Task Real_sink_with_default_levels_stays_off()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        var sink = new SdetStatementSink();
        var options = fixture.NewOptions();
        options.Diagnostics.Sink = sink;
        await using var context = await OrmLite.OpenAsync(options);
        var query = new Query<BehaveNote>(context, BehaveNote.Map).Where(n => n.TenantId == tenant);
        Assert.Equal(5, (await StreamingProviderChecks.CollectAsync(query.StreamAsync())).Count);
        Assert.Empty(sink.Statements);
        Assert.Empty(sink.Transactions);
    }

    [Fact(DisplayName = "[UC-010][AC06] native provider failure leaves caller transaction reconciliation to caller")]
    public async Task Borrowed_failure_does_not_repair_or_replace_transaction()
    {
        fixture.RequireAvailable();
        OrmMeta.Register<FinalMissingRow>(EntityDescriptor.Of(nameof(FinalMissingRow), "stream_final_missing", true)
            .Property("Id", "id", ColumnType.Int32, false, true));
        await using var unit = await UnitOfWork.BeginAsync(fixture.Context);
        var connection = unit.Connection;
        var transaction = unit.Transaction;
        var e = new Query<FinalMissingRow>(fixture.Context, r => new FinalMissingRow(r.GetInt32(0)))
            .On(unit).StreamAsync().GetAsyncEnumerator();
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ProviderFailure, fault.Error);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Same(connection, unit.Connection);
        Assert.Same(transaction, unit.Transaction);
        Assert.False(unit.Committed);
        Assert.Equal(ConnectionState.Open, connection.State);
        await AssertTransactionOutcomeAsync(connection, transaction);
        await transaction.RollbackAsync();
        Assert.Equal(1, await FinalQueries.ScalarAsync(connection, null));
    }

    [Fact(DisplayName = "[UC-010][AC07] legacy concurrent use reaches the real driver's existing reader contract")]
    public async Task Legacy_reader_conflict_has_no_stream_global_drain()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        await using var unit = await UnitOfWork.BeginAsync(fixture.Context);
        var query = StreamingProviderChecks.Query(fixture, tenant).On(unit);
        var e = query.StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        if (fixture.ProviderId == "postgres")
            await Assert.ThrowsAsync<NpgsqlOperationInProgressException>(() => query.ToListAsync());
        else Assert.Equal(5, (await query.ToListAsync()).Count);
        Assert.False(e.Completion.IsCompleted);
        Assert.Equal(1, e.Current.Stars);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal(5, (await query.ToListAsync()).Count);
        Assert.False(unit.Committed);
    }

    protected TFixture Fixture => fixture;

    private async Task AssertTransactionOutcomeAsync(
        System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction)
    {
        if (fixture.ProviderId == "postgres")
        {
            var aborted = await Assert.ThrowsAsync<PostgresException>(() =>
                FinalQueries.ScalarAsync(connection, transaction));
            Assert.Equal("25P02", aborted.SqlState);
        }
        else Assert.Equal(1, await FinalQueries.ScalarAsync(connection, transaction));
    }
}
