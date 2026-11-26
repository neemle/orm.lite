using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphChapter
{
    public static async Task<bool> RunAsync(OrmContext db)
    {
        var fixture = new GraphFixture();
        await fixture.SeedAsync(db);
        var ordinary = await Page(db).ToListAsync();
        var plain = ordinary.All(a => a.Books.Count == 0 && a.Tags.Count == 0);
        var roots = await Page(db).ToGraphListAsync(Request(20));
        var page = roots.Select(a => a.Id).SequenceEqual(ordinary.Select(a => a.Id));
        var membership = Membership(roots, fixture);
        var identity = SharedIdentity(roots, fixture);
        var nullable = roots.SelectMany(a => a.Books).Single(b => b.Id == fixture.Books[1].Id).Editor is null;
        var explicitPaths = roots.SelectMany(a => a.Books).All(b => b.Author is null);
        var refused = await RefuseOverflowAsync(db);
        var cleaned = await fixture.CleanupAsync(db);
        Console.WriteLine($"16a. requested graph: ordinary root page {page}, exact books and tags {membership}");
        Console.WriteLine($"     ordinary query left navigations empty {plain}");
        Console.WriteLine($"     shared editor/tag identity {identity}, nullable editor {nullable}, "
            + $"unrequested Author untouched {explicitPaths}");
        Console.WriteLine("     fixture bounds: depth 2, paths 3, nodes 3, roots 2, related rows 20, statements 8");
        Console.WriteLine($"     row overflow returned no list {refused}, rows cleaned {cleaned}");
        return page && plain && membership && identity && nullable && explicitPaths && refused && cleaned;
    }

    private static Query<GraphAuthor> Page(OrmContext db) => GraphAuthorOrm.Query(db)
        .Where(a => a.Name != "0 excluded").OrderBy(a => a.Name).Skip(1).Take(2);

    private static GraphRequest<GraphAuthor> Request(int relatedRows) => new(
        new GraphLimits(new GraphTraversalLimits(2, 3, 3), new GraphWorkLimits(2, relatedRows, 8)),
        GraphAuthorOrm.Paths.Books, GraphAuthorOrm.Paths.Tags,
        GraphAuthorOrm.Paths.Books.Then(GraphBookOrm.Paths.Editor));

    private static bool Membership(List<GraphAuthor> roots, GraphFixture fixture)
    {
        if (roots.Count != 2) return false;
        return roots[0].Id == fixture.Authors[2].Id && roots[1].Id == fixture.Authors[3].Id
            && roots.All(a => a.Books.All(b => b.AuthorId == a.Id))
            && roots.All(a => a.Books.Select(b => b.Id).ToHashSet().SetEquals(
                fixture.Books.Where(b => b.AuthorId == a.Id).Select(b => b.Id)))
            && roots[0].Tags.Select(t => t.Id).ToHashSet().SetEquals(fixture.Tags.Select(t => t.Id))
            && roots[1].Tags.Single().Id == fixture.Tags[0].Id;
    }

    private static bool SharedIdentity(List<GraphAuthor> roots, GraphFixture fixture)
    {
        if (roots.Count != 2) return false;
        var edited = roots[0].Books.Single(b => b.Id == fixture.Books[0].Id);
        var shared = roots[0].Tags.Single(t => t.Id == fixture.Tags[0].Id);
        return ReferenceEquals(roots[1], edited.Editor)
            && ReferenceEquals(roots[1], roots[1].Books.Single().Editor)
            && ReferenceEquals(shared, roots[1].Tags.Single());
    }

    private static async Task<bool> RefuseOverflowAsync(OrmContext db)
    {
        List<GraphAuthor>? published = null;
        try
        {
            published = await Page(db).ToGraphListAsync(Request(1));
        }
        catch (GraphLimitExceededException error) when (error.Budget == GraphBudget.RelatedRows)
        {
            return published is null;
        }
        return false;
    }
}
