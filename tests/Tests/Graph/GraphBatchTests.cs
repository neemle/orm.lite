using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphBatchTests
{
    [Fact(DisplayName = "[AC-108-04][SEC-108-02] bind chunks reserve mask parameters and deduplicate frontier keys")]
    public async Task Chunking_reserves_policy_parameters()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var bounded = new OrmContext(db.Context.DataSource, new GraphBoundedProvider(2));
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(),
            GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        var roots = await new Query<GraphAuthor>(bounded, GraphDomain.MapAuthor)
            .RedactWhen(a => a.Name, a => a.Id == 2).ToGraphListAsync(request);
        Assert.Equal(5, db.Statements.Count);
        Assert.All(db.Statements, s => Assert.InRange(s.ParameterCount, 0, 2));
        Assert.Same(roots[1], roots[0].Books[0].Editor);
        Assert.Null(roots[1].Name);
        Assert.Null(roots[0].Books[0].Editor!.Name);
    }

    [Fact(DisplayName = "[AC-108-04] many-to-many association reads batch source keys and deduplicate shared targets")]
    public async Task Link_source_and_target_batches_are_bounded()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var bounded = new OrmContext(db.Context.DataSource, new GraphBoundedProvider(2));
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorTags);
        var roots = await new Query<GraphAuthor>(bounded, GraphDomain.MapAuthor).ToGraphListAsync(request);
        Assert.Equal(4, db.Statements.Count);
        Assert.All(db.Statements, s => Assert.InRange(s.ParameterCount, 0, 2));
        Assert.Same(roots[0].Tags.Single(), roots[1].Tags.Single());
    }

    [Fact(DisplayName = "[UC-002 G3] zero remaining bind capacity refuses before root I/O")]
    public async Task Required_policy_parameters_cannot_consume_the_frontier_slot()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using var bounded = new OrmContext(db.Context.DataSource, new GraphBoundedProvider(1));
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(),
            GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        var error = await Assert.ThrowsAsync<GraphRequestException>(() =>
            new Query<GraphAuthor>(bounded, GraphDomain.MapAuthor).RedactWhen(a => a.Name, a => a.Id == 2)
                .ToGraphListAsync(request));
        Assert.Equal(GraphRequestError.NoBindCapacity, error.Error);
        Assert.Empty(db.Statements);
    }

    [Fact(DisplayName = "[AC-108-08] many-to-many association rows count even when targets are deleted or shared")]
    public async Task Association_rows_consume_the_related_row_budget()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(rows: 3), GraphDomain.AuthorTags);
        var error = await Assert.ThrowsAsync<GraphLimitExceededException>(() => db.Authors().ToGraphListAsync(request));
        Assert.Equal(GraphBudget.RelatedRows, error.Budget);
        Assert.Equal(3, db.Statements.Count);
    }
    [Fact(DisplayName = "[AC-108-03] null and missing link targets never become a default-key association")]
    public async Task Nullable_link_target_does_not_attach_a_zero_key_row()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await using (var connection = await db.Context.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO graph_tags VALUES (0, 'zero', NULL); "
                + "INSERT INTO graph_author_tags VALUES (1, NULL), (1, 999)";
            await command.ExecuteNonQueryAsync();
        }
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorTags);
        var roots = await db.Authors().Where(a => a.Id == 1).ToGraphListAsync(request);
        Assert.Equal(30, Assert.Single(roots.Single().Tags).Id);
    }

}
