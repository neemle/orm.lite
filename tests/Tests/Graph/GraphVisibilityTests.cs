using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphVisibilityTests
{
    [Fact(DisplayName = "[SEC-108-01] a masked nullable reference key refuses even when the condition is false")]
    public async Task Reference_frontier_key_masks_are_preflighted()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphBook>(GraphDomain.Limits(), GraphDomain.BookEditor);
        var query = new Query<GraphBook>(db.Context, GraphDomain.MapBook)
            .RedactWhen(b => b.EditorId, b => b.Id == -1);
        var error = await Assert.ThrowsAsync<GraphVisibilityException>(() => query.ToGraphListAsync(request));
        Assert.Equal("EditorId", error.Property);
        Assert.Equal("Editor", error.Path);
        Assert.Empty(db.Statements);
    }

    [Fact(DisplayName = "[SEC-108-01] a key needed only by an alternate nested revisit refuses before any I/O")]
    public async Task Later_inverse_key_requirements_are_preflighted_before_the_root()
    {
        await using var db = await GraphDatabase.OpenAsync();
        var request = new GraphRequest<GraphBook>(GraphDomain.Limits(),
            GraphDomain.BookEditor.Then(GraphDomain.AuthorBooks));
        var query = new Query<GraphBook>(db.Context, GraphDomain.MapBook).Redact(b => b.AuthorId);
        var error = await Assert.ThrowsAsync<GraphVisibilityException>(() => query.ToGraphListAsync(request));
        Assert.Equal("AuthorId", error.Property);
        Assert.Equal("Books", error.Path);
        Assert.Empty(db.Statements);
    }
}
