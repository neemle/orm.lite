using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public abstract class SdetProviderTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    [Fact(DisplayName = "[UC-010][SEC] actual provider late mapper fault emits no private row or parameter event")]
    public async Task Diagnostics_are_execute_only_and_values_are_private_by_default()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        var sink = new SdetStatementSink();
        var options = fixture.NewOptions();
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Information;
        await using var context = await OrmLite.OpenAsync(options);
        var maps = 0;
        var primary = new IOException("private-consumer-password");
        var q = new Query<BehaveNote>(context, r => ++maps == 2 ? throw primary : BehaveNote.Map(r))
            .Where(n => n.TenantId == tenant).OrderBy(n => n.Stars);
        var e = q.StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var recorded = Assert.Single(sink.Statements);
        Assert.Null(recorded.Parameters);
        Assert.Null(recorded.Error);
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        await e.DisposeAsync();
        Assert.Single(sink.Statements);
        Assert.Same(primary, (await e.Completion).PrimaryError);
        Assert.Equal(1, (await e.Completion).RowsYielded);
        Assert.DoesNotContain(tenant.ToString(), recorded.ToString());
        Assert.DoesNotContain("private-consumer-password", recorded + error.ToString() + await e.Completion);
    }

    [Fact(DisplayName = "[UC-010] actual provider projection columns freeze and typed NULL preserves default semantics")]
    public async Task Projection_capture_and_nullable_scalar_match_buffered_results()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        var q = StreamingProviderChecks.Query(fixture, tenant);
        Expression<Func<BehaveNote, object?>>[] columns = [n => n.Stars];
        var stream = q.StreamProjectAsync(columns, r => r.GetInt32(0));
        columns[0] = n => n.Title;
        Assert.Equal(new[] { 1, 2, 3, 4, 6 }, await StreamingProviderChecks.CollectAsync(stream));
        var masked = StreamingProviderChecks.Query(fixture, tenant).Redact(n => n.Stars);
        Assert.Equal(Enumerable.Repeat(0, 5),
            await StreamingProviderChecks.CollectAsync(masked.StreamColumnAsync<int>(n => n.Stars)));
        Assert.Equal(Enumerable.Repeat<int?>(null, 5),
            await StreamingProviderChecks.CollectAsync(masked.StreamColumnAsync<int?>(n => n.Stars)));
    }

    [Fact(DisplayName = "[UC-010] actual view stream uses shared view metadata and detached rows")]
    public async Task View_projection_stream_has_buffered_parity()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        OrmMeta.Register<SdetStreamView>(EntityDescriptor.Of(nameof(SdetStreamView), "sdet_stream_view", true)
            .Property("Stars", "stars", ColumnType.Int32, false, false).View());
        await fixture.ExecuteAsync("CREATE VIEW sdet_stream_view AS SELECT stars FROM behave_notes");
        try
        {
            var q = new Query<SdetStreamView>(fixture.Context, r => new SdetStreamView(r.GetInt32(0)))
                .OrderBy(v => v.Stars);
            var buffered = await q.ToListAsync();
            Assert.Equal(buffered, await StreamingProviderChecks.CollectAsync(q.StreamAsync()));
            Assert.Contains(new SdetStreamView(5), buffered);
        }
        finally { await fixture.ExecuteAsync("DROP VIEW sdet_stream_view"); }
    }

    [Fact(DisplayName = "[UC-010] actual borrowed transaction lock support matches provider and stays caller owned")]
    public async Task Locks_use_captured_transaction_and_cleanup_keeps_it_open()
    {
        fixture.RequireAvailable();
        var tenant = await StreamingProviderChecks.SeedAsync(fixture);
        await using var unit = await UnitOfWork.BeginAsync(fixture.Context);
        if (fixture.ProviderId == "sqlite")
        {
            Assert.ThrowsAny<Exception>(() => StreamingProviderChecks.Query(fixture, tenant).On(unit).ForUpdate());
            return;
        }
        var e = StreamingProviderChecks.Query(fixture, tenant).On(unit).ForUpdate().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        await e.DisposeAsync();
        Assert.Equal(QueryStreamStopReason.Disposed, (await e.Completion).Reason);
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.False(unit.Committed);
        await using var command = unit.Connection.CreateCommand();
        command.Transaction = unit.Transaction;
        command.CommandText = "SELECT 1";
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
}
