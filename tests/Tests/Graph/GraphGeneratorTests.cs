using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphGeneratorTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Neemle.OrmLite.Generated;
        [Table("graph_authors", Managed = false)]
        public class Author
        {
            [Key] public int Id { get; set; }
            public string? Name { get; set; }
            public List<Book> Books { get; set; } = new();
            [ManyToMany("graph_author_tags", "author_id", "tag_id")]
            public List<Tag> Tags { get; set; } = new();
        }
        [Table("graph_books", Managed = false)]
        public class Book
        {
            [Key] public int Id { get; set; }
            public int AuthorId { get; set; }
            public Author? Author { get; set; }
            public int? EditorId { get; set; }
            public Author? Editor { get; set; }
        }
        [Table("graph_tags", Managed = false)]
        public class Tag
        {
            [Key] public int Id { get; set; }
            public string? Name { get; set; }
        }
        public static class GraphOps
        {
            public static async Task<string> Read(OrmContext db)
            {
                var limits = new GraphLimits(new GraphTraversalLimits(3, 3, 4), new GraphWorkLimits(3, 30, 10));
                var request = new GraphRequest<Author>(limits, AuthorOrm.Paths.Books,
                    AuthorOrm.Paths.Tags, AuthorOrm.Paths.Books.Then(BookOrm.Paths.Editor));
                var roots = await AuthorOrm.Query(db).OrderBy(a => a.Id).Take(2).ToGraphListAsync(request);
                return string.Join(";", roots.Select(a => a.Id + ":" + a.Books.Count + ":" + a.Tags.Count))
                    + ":same-editor=" + ReferenceEquals(roots[1], roots[0].Books[0].Editor);
            }
        }
        """;

    [Fact(DisplayName = "[AC-108-01/06] generated typed paths compile and load the requested real SQLite graph")]
    public async Task Generated_consumer_uses_only_static_tokens_and_accessors()
    {
        var (diagnostics, generated, output) = Run(Source);
        Assert.Empty(diagnostics);
        Assert.Contains("class Paths", generated);
        Assert.Contains("GraphCollectionBinding", generated);
        Assert.Contains("GraphReferenceBinding", generated);
        Assert.Contains("GraphLinkBinding", generated);
        Assert.DoesNotContain("dynamic", generated);
        var model = GeneratedModel.Of(output);
        model.RunModuleInitializer();
        await using var db = await GraphDatabase.OpenAsync();
        var operations = model.Class("AuthorOrm").Assembly.GetType("Fixture.GraphOps", true)!;
        var task = (Task<string>)operations.GetMethod("Read")!.Invoke(null, [db.Context])!;
        Assert.Equal("1:2:2;2:2:1:same-editor=True", await task);
        Assert.Equal(5, db.Statements.Count);
    }
}
