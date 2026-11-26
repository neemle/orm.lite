using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphNavigationInputTests
{
    public static IEnumerable<object[]> InvalidNavigations() =>
        InvalidReferences().Concat(InvalidCollections()).Concat(InvalidLinks());

    private static IEnumerable<object[]> InvalidReferences()
    {
        yield return Case("reference reader", () =>
            new GraphReferenceBinding<GraphBook, GraphAuthor, int>("Editor", "editor_id", null!, (_, _) => { }));
        yield return Case("reference assign", () =>
            new GraphReferenceBinding<GraphBook, GraphAuthor, int>("Editor", "editor_id", _ => new(true, 1), null!));
        yield return Case("reference name", () =>
            new GraphReferenceBinding<GraphBook, GraphAuthor, int>(" ", "editor_id", _ => new(true, 1), (_, _) => { }));
        yield return Case("reference column", () =>
            new GraphReferenceBinding<GraphBook, GraphAuthor, int>("Editor", "", _ => new(true, 1), (_, _) => { }));
    }

    private static IEnumerable<object[]> InvalidCollections()
    {
        yield return Case("collection source", () =>
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("Books", "author_id", null!,
                _ => new(true, 1), (_, _) => { }));
        yield return Case("collection target", () =>
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("Books", "author_id", _ => 1, null!, (_, _) => { }));
        yield return Case("collection assign", () =>
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("Books", "author_id", _ => 1,
                _ => new(true, 1), null!));
        yield return Case("collection name", () =>
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("", "author_id", _ => 1,
                _ => new(true, 1), (_, _) => { }));
        yield return Case("collection column", () =>
            new GraphCollectionBinding<GraphAuthor, GraphBook, int>("Books", " ", _ => 1,
                _ => new(true, 1), (_, _) => { }));
    }

    private static IEnumerable<object[]> InvalidLinks()
    {
        yield return Case("link metadata", () =>
            new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags", null!, _ => 1, (_, _) => { }));
        yield return Case("link reader", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
            new("links", "from_id", "to_id"), null!, (_, _) => { }));
        yield return Case("link assign", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
            new("links", "from_id", "to_id"), _ => 1, null!));
        yield return Case("link name", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>(" ",
            new("links", "from_id", "to_id"), _ => 1, (_, _) => { }));
        yield return Case("link table", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
            new("", "from_id", "to_id"), _ => 1, (_, _) => { }));
        yield return Case("link source column", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
            new("links", "", "to_id"), _ => 1, (_, _) => { }));
        yield return Case("link target column", () => new GraphLinkBinding<GraphAuthor, GraphTag, int, int>("Tags",
            new("links", "from_id", " "), _ => 1, (_, _) => { }));
    }

    private static object[] Case(string name, Action create) => [name, create];

    [Theory(DisplayName = "[AC-108-05] incomplete trusted navigation metadata never becomes a valid binding")]
    [MemberData(nameof(InvalidNavigations))]
    public void Null_callbacks_and_blank_endpoint_names_are_rejected(string name, Action create)
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(GraphRequestError.InvalidBinding, Assert.Throws<GraphRequestException>(create).Error);
    }

    [Fact(DisplayName = "[AC-108-05] root registration needs one nonnullable key and distinct navigations")]
    public void Invalid_entity_shapes_cannot_be_registered()
    {
        var missing = EntityDescriptor.Of("Author", "authors", false);
        var nullable = EntityDescriptor.Of("Author", "authors", false)
            .Property("Id", "id", ColumnType.Int32, true, true);
        var composite = EntityDescriptor.Of("Author", "authors", false)
            .Property("Id", "id", ColumnType.Int32, false, true)
            .Property("Other", "other", ColumnType.Int32, false, true);
        var key = new GraphKeyBinding<GraphAuthor, int>("id", author => author.Id);
        foreach (var descriptor in new[] { missing, nullable, composite })
            Assert.Equal(GraphRequestError.InvalidBinding, Assert.Throws<GraphRequestException>(() =>
                new GraphEntityBinding<GraphAuthor>(descriptor, GraphDomain.MapAuthor, key)).Error);
        var navigation = new GraphReferenceBinding<GraphAuthor, GraphAuthor, int>("Other", "id",
            author => new(true, author.Id), (_, _) => { });
        Assert.Equal(GraphRequestError.InvalidBinding, Assert.Throws<GraphRequestException>(() =>
            new GraphEntityBinding<GraphAuthor>(GraphDomain.Authors, GraphDomain.MapAuthor, key,
                navigation, navigation)).Error);
    }
}
