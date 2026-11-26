using System.Data;
using System.Data.Common;
using Neemle.OrmLite.Tests.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph.Security;

[Collection("Graph")]
public sealed class GraphCommandTimeoutTests
{
    [Theory(DisplayName = "[SEC][UC-002] an awaited injected SQLite command timeout publishes no graph or retry")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sqlite_command_timeout_cleans_owned_work_and_preserves_caller_work(bool callerOwned)
    {
        await using var db = await SecurityDatabase.SqliteAsync(true);
        await VerifyAsync(db, callerOwned);
    }

    [PostgresFact(DisplayName = "[SEC][UC-002] an awaited injected PostgreSQL command timeout preserves ownership")]
    public async Task Postgres_command_timeout_covers_owned_and_borrowed_connection_transactions()
    {
        foreach (var callerOwned in new[] { false, true })
        {
            await using var db = await SecurityDatabase.PostgresAsync(true);
            await VerifyAsync(db, callerOwned);
        }
    }

    private static async Task VerifyAsync(SecurityDatabase db, bool callerOwned)
    {
        db.Source.FailRelated = true;
        var query = SecurityModel.Query(db.Context);
        await using var connection = callerOwned ? await db.Context.DataSource.OpenConnectionAsync() : null;
        await using var transaction = connection is null ? null : await connection.BeginTransactionAsync();
        if (connection is not null) query.On(connection, transaction);
        List<SecurityRoot>? published = null;
        var request = new GraphRequest<SecurityRoot>(GraphDomain.Limits(), SecurityModel.Children);
        var actual = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            published = await query.ToGraphListAsync(request);
        });
        Assert.Same(db.Source.Timeout, actual);
        Assert.Null(published);
        Assert.Equal(2, SecurityModel.MappedRoots);
        Assert.Equal(2, db.Source.Reads.Count);
        Assert.True(Assert.Single(db.Source.Readers).IsClosed);
        Assert.All(db.Source.Commands, command => Assert.True(command.WasDisposed));
        Assert.Single(db.Sink.Statements, statement => ReferenceEquals(statement.Error, actual));
        SecurityAssertions.Privacy(db, true);
        await ResourcesAsync(db, connection, transaction);
        await db.AssertWitnessAsync();
    }

    private static async Task ResourcesAsync(SecurityDatabase db, DbConnection? connection, DbTransaction? transaction)
    {
        var tracked = Assert.Single(db.Source.Connections);
        Assert.Equal(connection is null, tracked.WasDisposed);
        Assert.All(db.Source.Reads, read => Assert.Same(transaction, read.Transaction));
        if (connection is null) { Assert.Equal(ConnectionState.Closed, tracked.State); return; }
        Assert.Same(tracked, connection);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Same(tracked.Inner, transaction!.Connection);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        await transaction.RollbackAsync();
    }
}
