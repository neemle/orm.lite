using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphDepthTests
{
    [Fact(DisplayName = "[AC-108-04/08] a deep explicit finite cycle executes serially within exact work limits")]
    public async Task Deep_explicit_cycles_do_not_require_recursive_execution()
    {
        await using var db = await GraphDatabase.OpenAsync();
        const int repetitions = 1000;
        var path = GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor);
        for (var index = 1; index < repetitions; index++)
            path = path.Then(GraphDomain.AuthorBooks).Then(GraphDomain.BookEditor);
        var request = new GraphRequest<GraphAuthor>(new(new(2 * repetitions, 1, 2 * repetitions),
            new(1, 3 * repetitions, 1 + 2 * repetitions)), path);
        var roots = await db.Authors().Where(a => a.Id == 2).ToGraphListAsync(request);
        var root = Assert.Single(roots);
        Assert.Equal(2, root.Books.Count);
        Assert.Same(root, root.Books.Single(book => book.Id == 12).Editor);
        Assert.Null(root.Books.Single(book => book.Id == 13).Editor);
        Assert.Empty(root.Tags);
        Assert.Equal(1 + 2 * repetitions, db.Statements.Count);
        Assert.Single(db.Statements.Select(statement => statement.ConnectionId).Distinct());
    }

    [Fact(DisplayName = "[UC-002 G3] a small node budget refuses a deep path during finite preflight")]
    public async Task A_small_node_budget_refuses_without_opening_a_root_reader()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var path = GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor);
        for (var index = 1; index < 1000; index++)
            path = path.Then(GraphDomain.AuthorBooks).Then(GraphDomain.BookEditor);
        var request = new GraphRequest<GraphAuthor>(new(new(2000, 1, 1), new(3, 3000, 2001)), path);
        var error = await Assert.ThrowsAsync<GraphLimitExceededException>(() => db.Authors().ToGraphListAsync(request));
        Assert.Equal(GraphBudget.Nodes, error.Budget);
        Assert.Empty(db.Statements);
    }
}
