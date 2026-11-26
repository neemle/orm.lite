using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class ReadCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        var roots = await AuthorOrm.Query(f.Context).OrderBy(a => a.Id).Take(2).ToListAsync();
        Check.That(roots.Select(a => a.Id).SequenceEqual(new[] { 1, 2 }), "root page");
        Check.That(roots[0].Books.Select(b => b.Id).SequenceEqual(new[] { 10, 11 }), "eager collection");
        Check.That(ReferenceEquals(roots[0], roots[0].Books[0].Author), "eager reference identity cycle");
        Check.That(roots.All(a => a.Tags.Count == 0), "lazy zero demand");
        Check.That(f.Sink.Count == 3, "batched root+book+author statement count");
        f.Sink.Clear();
        var one = await AuthorOrm.Query(f.Context).Where(a => a.Id == 1).ToListAsync();
        Check.That(f.Sink.Count == 3 && one[0].Books.Count == 2, "count constant inside chunk");
        f.Sink.Clear();
        var suppressed = await AuthorOrm.Query(f.Context).ToListAsync(new(EagerBehavior.Suppress));
        Check.That(suppressed.Count == 3 && suppressed.All(a => a.Books.Count == 0), "invocation suppress");
        Check.That(f.Sink.Count == 1, "suppress no relation statements");
        f.Sink.Clear();
        Check.That((await AuthorOrm.GetAsync(f.Context, 1))!.Books.Count == 2, "ordinary generated keyed eager");
        Check.That(f.Sink.Count == 3, "keyed single root fetch");
        f.Sink.Clear();
        Check.That(await AuthorOrm.Query(f.Context).CountAsync() == 3 && f.Sink.Count == 1, "scalar unchanged");
        f.Sink.Clear();
        Check.That((await PlainOrm.Query(f.Context).ToListAsync()).Count == 2 && f.Sink.Count == 1, "zero-policy branch");
    }
}
