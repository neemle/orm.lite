using System.Data;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphOwnedResourceTests
{
    [Fact(DisplayName = "[AC-108-08] owned connection is disposed when a related mapper fails")]
    public async Task Mapper_failure_preserves_the_exception_and_disposes_the_owned_connection()
    {
        await using var fixture = await GraphDatabase.OpenAsync();
        await using var source = new SdetTrackedSource(fixture.Context.DataSource.ConnectionString);
        await using var db = new OrmContext(source, SqliteProvider.Instance);
        var injected = new InvalidOperationException("SDET mapper failure");
        GraphDomain.BookMapError = injected;
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
            Assert.Same(injected, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToGraphListAsync(request)));
            Assert.True(Assert.Single(source.Connections).WasDisposed);
            Assert.Equal(ConnectionState.Closed, source.Connections[0].State);
        }
        finally { GraphDomain.BookMapError = null; }
        Assert.Equal(3, (await new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToListAsync()).Count);
        Assert.All(source.Connections, connection => Assert.True(connection.WasDisposed));
    }

    [Fact(DisplayName = "[AC-108-08] owned connection closes on cancellation between root and relation reads")]
    public async Task Cancellation_releases_owned_resources_before_returning_failure()
    {
        await using var fixture = await GraphDatabase.OpenAsync();
        await using var source = new SdetTrackedSource(fixture.Context.DataSource.ConnectionString);
        await using var db = new OrmContext(source, SqliteProvider.Instance);
        using var cancellation = new CancellationTokenSource();
        GraphDomain.RootReadHook = cancellation.Cancel;
        try
        {
            var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToGraphListAsync(request, cancellation.Token));
            Assert.True(Assert.Single(source.Connections).WasDisposed);
            Assert.Equal(ConnectionState.Closed, source.Connections[0].State);
        }
        finally { GraphDomain.RootReadHook = null; }
    }

    [Fact(DisplayName = "[AC-108-08] provider failure disposes the graph-owned connection without a replacement")]
    public async Task Provider_failure_does_not_leak_or_reacquire_the_owned_connection()
    {
        await using var fixture = await GraphDatabase.OpenAsync();
        await using (var command = fixture.Context.DataSource.CreateCommand("DROP TABLE graph_books"))
            await command.ExecuteNonQueryAsync();
        await using var source = new SdetTrackedSource(fixture.Context.DataSource.ConnectionString);
        await using var db = new OrmContext(source, SqliteProvider.Instance);
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        await Assert.ThrowsAsync<SqliteException>(() =>
            new Query<GraphAuthor>(db, GraphDomain.MapAuthor).ToGraphListAsync(request));
        Assert.True(Assert.Single(source.Connections).WasDisposed);
        Assert.Equal(ConnectionState.Closed, source.Connections[0].State);
    }
}
