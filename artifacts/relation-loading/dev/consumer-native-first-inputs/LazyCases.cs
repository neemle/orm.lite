using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using System.Text.Json;

namespace PolicySmoke;

internal static class LazyCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var roots = await AuthorOrm.Query(f.Context).OrderBy(a => a.Id)
            .ToListAsync(new(EagerBehavior.Suppress, scope));
        var author = roots[0];
        Check.That(f.Sink.Count == 1, "scoped suppressed root only");
        Check.That(scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.Unloaded, "unloaded state");
        _ = author.Tags.Count;
        _ = JsonSerializer.Serialize(author, SmokeJsonContext.Default.Author);
        Check.That(f.Sink.Count == 1, "getter and source-generated serialization zero lazy IO");
        await scope.LoadAsync(author, AuthorOrm.Paths.Tags);
        Check.That(author.Tags.Count == 2 && f.Sink.Count == 3, "explicit lazy link two statements");
        Check.That(scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.LoadedValue, "loaded value");
        await scope.LoadAsync(author, AuthorOrm.Paths.Tags);
        Check.That(f.Sink.Count == 3, "successful demand cached");
        await scope.LoadAsync(roots[2], AuthorOrm.Paths.Tags);
        Check.That(scope.State(roots[2], AuthorOrm.Paths.Tags).State == RelationLoadState.LoadedEmpty, "empty distinct");
        var before = f.Sink.Count;
        scope.Detach(author);
        Check.That(!scope.State(author, AuthorOrm.Paths.Tags).IsAttached && author.Tags.Count == 2, "detach retains values");
        Check.That((await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags)))
            .Error == RelationScopeError.Detached && f.Sink.Count == before, "detached no SQL");
    }
}
