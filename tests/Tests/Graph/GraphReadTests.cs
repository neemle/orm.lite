using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphReadTests
{
    [Fact(DisplayName = "[UC-002 G1/G2] books, shared tags and nested editor preserve identity and membership")]
    public async Task Full_graph_batches_prefixes_and_reuses_identity()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks,
            GraphDomain.AuthorTags, GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        var roots = await db.Authors().OrderBy(a => a.Id).ToGraphListAsync(request);
        Assert.Equal(new[] { 1, 2, 3 }, roots.Select(a => a.Id));
        Assert.Equal(new[] { 10, 11 }, roots[0].Books.Select(b => b.Id));
        Assert.Equal(new[] { 12, 13 }, roots[1].Books.Select(b => b.Id));
        Assert.Empty(roots[2].Books);
        Assert.Empty(roots[2].Tags);
        Assert.Same(roots[1], roots[0].Books[0].Editor);
        Assert.Null(roots[0].Books[1].Editor);
        Assert.Null(roots[1].Books[1].Editor);
        Assert.Single(roots[0].Tags);
        Assert.Same(roots[0].Tags[0], roots[1].Tags.Single());
        Assert.Equal(5, db.Statements.Count);
        Assert.Single(db.Statements.Select(s => s.ConnectionId).Distinct());
        Assert.All(db.Statements, s => Assert.Equal(StatementKind.Read, s.Kind));
    }

    [Fact(DisplayName = "[UC-002 G1] root filter, order and page stay root-first")]
    public async Task Ordinary_page_and_empty_roots_are_preserved()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        var roots = await db.Authors().Where(a => a.Id >= 1).OrderBy(a => a.Id).Skip(1).Take(1)
            .ToGraphListAsync(request);
        Assert.Equal(2, Assert.Single(roots).Id);
        Assert.Equal(new[] { 12, 13 }, roots[0].Books.Select(b => b.Id));
        Assert.Equal(2, db.Statements.Count);
        db.Statements.Clear();
        Assert.Empty(await db.Authors().Where(a => a.Id == 999).ToGraphListAsync(request));
        Assert.Single(db.Statements);
    }

    [Fact(DisplayName = "[SEC-108-02] scalar masks survive a nested revisit of the root type")]
    public async Task Root_type_masks_are_reused_without_unmasking_identity()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(),
            GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        var roots = await db.Authors().Where(a => a.Id == 1).RedactWhen(a => a.Name, a => a.Id == 2)
            .ToGraphListAsync(request);
        Assert.Equal("Ada", roots.Single().Name);
        Assert.Null(roots[0].Books[0].Editor!.Name);
    }

    [Fact(DisplayName = "[SEC-108-01] conditional and unconditional identity masks refuse before root I/O")]
    public async Task Required_key_masks_refuse_without_statements()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        await Assert.ThrowsAsync<GraphVisibilityException>(() =>
            db.Authors().Redact(a => a.Id).ToGraphListAsync(request));
        await Assert.ThrowsAsync<GraphVisibilityException>(() =>
            db.Authors().RedactWhen(a => a.Id, a => a.Id == 99).ToGraphListAsync(request));
        Assert.Empty(db.Statements);
    }

    [Fact(DisplayName = "[UC-002 G3/G4] root, row and statement bounds fail without partial return")]
    public async Task Work_budgets_refuse_at_the_boundary()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var root = new GraphRequest<GraphAuthor>(GraphDomain.Limits(roots: 2));
        Assert.Equal(GraphBudget.Roots, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(root))).Budget);
        db.Statements.Clear();
        await Assert.ThrowsAsync<GraphLimitExceededException>(() => db.Authors().Take(3).ToGraphListAsync(root));
        Assert.Empty(db.Statements);
        var related = new GraphRequest<GraphAuthor>(GraphDomain.Limits(rows: 3), GraphDomain.AuthorBooks);
        Assert.Equal(GraphBudget.RelatedRows, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(related))).Budget);
        var statements = new GraphRequest<GraphAuthor>(GraphDomain.Limits(statements: 1), GraphDomain.AuthorBooks);
        Assert.Equal(GraphBudget.Statements, (await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().ToGraphListAsync(statements))).Budget);
    }

    [Fact(DisplayName = "[UC-002 G4] external connection and transaction remain owned by the caller")]
    public async Task Caller_transaction_is_preserved_on_success_and_failure()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var connection = await db.Context.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorBooks);
        Assert.Equal(3, (await db.Authors().On(connection, transaction).ToGraphListAsync(request)).Count);
        var limited = new GraphRequest<GraphAuthor>(GraphDomain.Limits(rows: 1), GraphDomain.AuthorBooks);
        await Assert.ThrowsAsync<GraphLimitExceededException>(() =>
            db.Authors().On(connection, transaction).ToGraphListAsync(limited));
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.Same(connection, transaction.Connection);
        await transaction.RollbackAsync();
    }
}
