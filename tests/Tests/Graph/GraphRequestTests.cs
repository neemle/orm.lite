using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphRequestTests
{
    [Fact(DisplayName = "[UC-002 G3] limits reject zero/negative values and requests own their path array")]
    public void Limits_and_paths_are_immutable_snapshots()
    {
        GraphDomain.Register();
        for (var index = 0; index < 6; index++)
        {
            var values = Enumerable.Repeat(1, 6).ToArray();
            values[index] = 0;
            var error = Assert.Throws<GraphRequestException>(() => new GraphLimits(
                new(values[0], values[1], values[2]), new(values[3], values[4], values[5])));
            Assert.Equal(GraphRequestError.InvalidLimits, error.Error);
        }
        GraphPath<GraphAuthor>[] paths = [GraphDomain.AuthorBooks];
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), paths);
        paths[0] = GraphDomain.AuthorTags;
        Assert.Same(GraphDomain.AuthorBooks.Edges[0], request.Paths[0].Edges[0]);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<GraphPath<GraphAuthor>>>(request.Paths).Clear());
    }

    [Fact(DisplayName = "[SEC-108-01] hostile path names are refused without echoing request text")]
    public void Paths_are_bound_to_exact_registered_navigation_and_target()
    {
        GraphDomain.Register();
        const string hostile = "Books; SELECT secret FROM passwords";
        var error = Assert.Throws<GraphRequestException>(() => GraphPaths.Edge<GraphAuthor, GraphBook>(hostile));
        Assert.Equal(GraphRequestError.InvalidPath, error.Error);
        Assert.Null(error.Path);
        Assert.DoesNotContain(hostile, error.Message);
        Assert.Throws<GraphRequestException>(() => GraphPaths.Edge<GraphAuthor, GraphTag>("Books"));
        var composed = GraphDomain.BookEditor.Then(GraphDomain.AuthorBooks);
        Assert.Throws<GraphRequestException>(() => GraphDomain.AuthorBooks.Then(composed));
    }

    [Fact(DisplayName = "[UC-002 G3] all canonical traversal budgets refuse before I/O")]
    public async Task Topology_and_projection_refusals_execute_no_statements()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var nested = GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor);
        var depth = new GraphRequest<GraphAuthor>(new(new(1, 10, 10), new(10, 100, 10)), nested);
        Assert.Equal(GraphBudget.Depth, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(depth))).Budget);
        var nodes = new GraphRequest<GraphAuthor>(new(new(10, 10, 1), new(10, 100, 10)), nested);
        Assert.Equal(GraphBudget.Nodes, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(nodes))).Budget);
        var paths = new GraphRequest<GraphAuthor>(new(new(10, 1, 10), new(10, 100, 10)),
            GraphDomain.AuthorBooks, GraphDomain.AuthorTags);
        Assert.Equal(GraphBudget.Paths, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(paths))).Budget);
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        Assert.Equal(GraphRequestError.UnsupportedShape, (await Assert.ThrowsAsync<GraphRequestException>(() =>
            db.Authors().ProjectColumn(a => a.Id).ToGraphListAsync(request))).Error);
        Assert.Empty(db.Statements);
    }

    [Fact(DisplayName = "[UC-002 G3] duplicate complete paths and shared prefixes have canonical budget accounting")]
    public async Task Duplicate_paths_execute_each_prefix_once()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(new(new(2, 1, 2), new(3, 10, 3)),
            GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor), GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        Assert.Equal(3, (await db.Authors().ToGraphListAsync(request)).Count);
        Assert.Equal(3, db.Statements.Count);
    }

    [Fact(DisplayName = "[UC-002 G4] pre-cancellation executes no statements and large finite limits do not overflow")]
    public async Task Cancellation_and_largest_limits_have_defined_boundaries()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(new(new(int.MaxValue, int.MaxValue, int.MaxValue),
            new(int.MaxValue, int.MaxValue, int.MaxValue)), GraphDomain.AuthorBooks);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            db.Authors().ToGraphListAsync(request, new CancellationToken(true)));
        Assert.Empty(db.Statements);
        Assert.Equal(3, (await db.Authors().ToGraphListAsync(request)).Count);
        Assert.All(db.Statements, statement =>
            Assert.DoesNotContain("LIMIT", statement.Text, StringComparison.OrdinalIgnoreCase));
    }
}
