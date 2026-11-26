using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class QueueCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options with { MaxPendingDemands = 1 });
        var roots = await AuthorOrm.Query(f.Context).OrderBy(a => a.Id).ToListAsync(new(EagerBehavior.Suppress, scope));
        var book = (await BookOrm.Query(f.Context).Where(b => b.Id == 10)
            .ToListAsync(new(EagerBehavior.Suppress, scope))).Single();
        using var pause = new ReadPause("policy_author_tags");
        f.Sink.Pause = pause;
        var active = Task.Run(() => scope.LoadAsync(roots[0], AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var queued = scope.LoadAsync(book, BookOrm.Paths.Editor);
        var full = await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(roots[2], AuthorOrm.Paths.Tags));
        Check.That(full.Error == RelationScopeError.QueueFull, "finite queue refuses overflow");
        book.EditorId = 1;
        var before = f.Sink.Count;
        pause.Release();
        await active.WaitAsync(TimeSpan.FromSeconds(10));
        var changed = await Check.ThrowsAsync<RelationScopeException>(() => queued);
        f.Sink.Pause = null;
        Check.That(changed.Error == RelationScopeError.EntityChanged && f.Sink.Count == before + 1,
            "queued key change refuses before its SQL");
        await scope.RetryAsync(roots[2], AuthorOrm.Paths.Tags);
        Check.That(scope.State(roots[2], AuthorOrm.Paths.Tags).State == RelationLoadState.LoadedEmpty,
            "queue refusal is explicit retryable fault");
    }
}
