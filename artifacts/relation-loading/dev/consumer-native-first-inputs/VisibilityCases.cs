using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class VisibilityCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var masked = (await AuthorOrm.Query(f.Context).Where(a => a.Id == 1).Redact(a => a.Name)
            .ToListAsync(new(Scope: scope))).Single();
        Check.That(masked.Name is null && masked.Books.All(b => b.Author?.Name is null), "mask-safe eager root revisit");
        var before = f.Sink.Count;
        await Check.ThrowsAsync<GraphVisibilityException>(() => AuthorOrm.Query(f.Context).Redact(a => a.Id).ToListAsync());
        Check.That(f.Sink.Count == before, "masked required root key pre-SQL");
        var books = await BookOrm.Query(f.Context).OrderBy(b => b.Id).ToListAsync(new(EagerBehavior.Suppress, scope));
        before = f.Sink.Count;
        await scope.LoadAsync(books[1], BookOrm.Paths.Editor);
        Check.That(f.Sink.Count == before && scope.State(books[1], BookOrm.Paths.Editor).State == RelationLoadState.LoadedEmpty,
            "nullable null reference is loaded empty with zero statement");
        books[0].EditorId = 1;
        var changed = await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(books[0], BookOrm.Paths.Editor));
        Check.That(changed.Error == RelationScopeError.EntityChanged && f.Sink.Count == before, "changed FK pre-SQL");
        var hidden = (await BookOrm.Query(f.Context).Where(b => b.Id == 10).Redact(b => b.EditorId)
            .ToListAsync(new(EagerBehavior.Suppress, scope))).Single();
        before = f.Sink.Count;
        await Check.ThrowsAsync<GraphVisibilityException>(() => scope.LoadAsync(hidden, BookOrm.Paths.Editor));
        Check.That(f.Sink.Count == before && hidden.Editor is null, "masked lazy FK pre-SQL");
    }
}
