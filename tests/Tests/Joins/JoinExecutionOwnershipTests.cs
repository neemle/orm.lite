using System.Data.Common;
using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinExecutionOwnershipTests
{
    [Fact] public async Task Owned_success_uses_one_connection_and_command_and_forwards_real_token()
    {
        var f = new JoinExecutionFixture(); using var token = new CancellationTokenSource();
        var rows = await f.Run(token.Token); Assert.Single(rows);
        Assert.Equal(1, f.Probe.Connections); Assert.Equal(1, f.Probe.Opens); Assert.Equal(1, f.Probe.Executes);
        Assert.Equal(token.Token, f.Probe.OpenToken); Assert.Equal(token.Token, f.Probe.ExecuteToken);
        Assert.Equal(token.Token, f.Probe.ReadToken); f.Owned();
        Assert.Equal(new[] { "reader", "command", "connection" }, f.Probe.Events);
    }
    [Fact] public async Task Borrowed_success_uses_exact_objects_and_never_settles_caller()
    {
        var f = new JoinExecutionFixture(); f.Bind(); Assert.Single(await f.Run());
        Assert.Same(f.BoundConnection, Assert.Single(f.Probe.Issued).Connection);
        Assert.Same(f.BoundTransaction, Assert.Single(f.Probe.Issued).Transaction);
        Assert.Equal(1, f.Probe.ReaderDisposals); Assert.Equal(1, f.Probe.CommandDisposals); f.Borrowed();
    }
    [Fact] public async Task Metadata_changed_during_open_refuses_before_command()
    {
        var f = new JoinExecutionFixture();
        f.Probe.Opening = _ =>
        {
            OrmMeta.Register<JoinUnitRow<JoinExecutionFixture>>(JoinUnitModel<JoinExecutionFixture>.Create());
            return Task.CompletedTask;
        };
        try
        {
            var error = await Assert.ThrowsAsync<JoinReadException>(() => f.Run());
            Assert.Equal(JoinError.UnsupportedSource, error.Error); Assert.Equal(0, f.Probe.Commands); f.Owned(0, 0);
        }
        finally { OrmMeta.Register<JoinUnitRow<JoinExecutionFixture>>(JoinUnitModel<JoinExecutionFixture>.Descriptor); }
    }
    [Fact] public async Task Policy_appearing_at_eof_discards_buffer_and_retains_borrowed_custody()
    {
        var f = new JoinExecutionFixture(); f.Bind();
        f.Probe.AfterRead = available =>
        {
            if (!available) PolicyReadHooks<JoinUnitRow<JoinExecutionFixture>>.Rows = static (_, _, _) =>
                throw new InvalidOperationException("policy must not run");
        };
        try
        {
            var error = await Assert.ThrowsAsync<JoinReadException>(() => f.Run());
            Assert.Equal(JoinError.UnsupportedVisibility, error.Error); Assert.Equal(2, f.Probe.Decodes); f.Borrowed();
            Assert.Equal(1, f.Probe.ReaderDisposals); Assert.Equal(1, f.Probe.CommandDisposals);
        }
        finally { PolicyReadHooks<JoinUnitRow<JoinExecutionFixture>>.Rows = null; }
    }
    [Fact] public async Task Concurrent_owned_runs_keep_handles_parameters_and_rows_separate()
    {
        var f = new JoinExecutionFixture(); var arrived = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Probe.Opening = ct =>
        {
            if (Interlocked.Increment(ref arrived) == 2) gate.SetResult();
            return gate.Task.WaitAsync(ct);
        };
        var query = f.Query().Where(f.Model.A.Column(x => x.Rank).GreaterThanValue(7));
        var first = query.ToListAsync(f.Model.Selection); var second = query.ToListAsync(f.Model.Selection);
        var results = await Task.WhenAll(first, second); Assert.All(results, rows => Assert.Single(rows));
        Assert.NotSame(results[0], results[1]); Assert.Equal(2, f.Probe.Connections);
        Assert.NotSame(f.Probe.Issued[0].Connection, f.Probe.Issued[1].Connection);
        Assert.NotSame(f.Probe.Issued[0].Parameters[0], f.Probe.Issued[1].Parameters[0]);
        Assert.Equal(7, f.Probe.Issued[0].Parameters[0].Value); Assert.Equal(7, f.Probe.Issued[1].Parameters[0].Value);
        Assert.Equal(2, f.Probe.ReaderDisposals); Assert.Equal(2, f.Probe.CommandDisposals);
        Assert.Equal(2, f.Probe.ConnectionDisposals);
        Assert.Equal(0, f.Probe.Transactions);
    }
}
