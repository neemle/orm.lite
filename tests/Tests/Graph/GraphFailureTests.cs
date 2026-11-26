using System.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphFailureTests
{
    [Fact(DisplayName = "[AC-108-08] relation mapper failure preserves the exception and caller unit of work")]
    public async Task Mapper_failure_releases_the_reader_and_does_not_end_the_unit_of_work()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var work = await UnitOfWork.BeginAsync(db.Context);
        var error = new InvalidOperationException("injected mapper failure");
        GraphDomain.BookMapError = error;
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.Authors().On(work).ToGraphListAsync(request));
            Assert.Same(error, actual);
            Assert.False(work.Committed);
            Assert.Equal(ConnectionState.Open, work.Connection.State);
            Assert.Same(work.Connection, work.Transaction.Connection);
            await using var command = work.Connection.CreateCommand();
            command.Transaction = work.Transaction;
            command.CommandText = "SELECT count(*) FROM graph_authors";
            Assert.Equal(3L, await command.ExecuteScalarAsync());
        }
        finally { GraphDomain.BookMapError = null; }
    }

    [Fact(DisplayName = "[AC-108-08] provider failure has no retry and keeps the external connection usable")]
    public async Task Related_provider_failure_is_not_hidden_or_retried()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var connection = await db.Context.DataSource.OpenConnectionAsync();
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = "DROP TABLE graph_books";
            await drop.ExecuteNonQueryAsync();
        }
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        await Assert.ThrowsAsync<SqliteException>(() => db.Authors().On(connection).ToGraphListAsync(request));
        Assert.Equal(2, db.Statements.Count);
        Assert.Single(db.Statements, s => s.Error is not null);
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Equal(3, (await db.Authors().On(connection).ToListAsync()).Count);
    }

    [Fact(DisplayName = "[AC-108-08] cancellation after a root map prevents relation I/O and partial return")]
    public async Task Cancellation_between_root_and_relation_stops_the_whole_operation()
    {
        await using var db = await GraphDatabase.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        GraphDomain.RootReadHook = cancellation.Cancel;
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                db.Authors().ToGraphListAsync(request, cancellation.Token));
            Assert.Single(db.Statements);
        }
        finally { GraphDomain.RootReadHook = null; }
        Assert.Equal(3, (await db.Authors().ToListAsync()).Count);
    }

    [Fact(DisplayName = "[AC-108-08] an invocation snapshots mutable query policy before opening the root reader")]
    public async Task Query_mutation_during_a_map_does_not_change_the_active_plan()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var query = db.Authors().Redact(a => a.Name);
        GraphDomain.RootReadHook = () => query.Redact(a => a.Id).Take(1);
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(),
                GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
            var roots = await query.ToGraphListAsync(request);
            Assert.Equal(3, roots.Count);
            Assert.All(roots, a => Assert.Null(a.Name));
            Assert.Same(roots[1], roots[0].Books[0].Editor);
            Assert.Null(roots[0].Books[0].Editor!.Name);
        }
        finally { GraphDomain.RootReadHook = null; }
    }
}
