using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class NestedDetachCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var books = await BookOrm.Query(f.Context).OrderBy(b => b.Id)
            .ToListAsync(new(EagerBehavior.Suppress, scope));
        var root = books.Single(b => b.Id == 10);
        var related = books.Single(b => b.Id == 12);
        var original = related.Author;
        using var pause = new ReadPause("policy_authors", 1);
        f.Sink.Pause = pause;
        try
        {
            var loading = Task.Run(() => scope.LoadAsync(root, BookOrm.Paths.Editor));
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Check.That(f.Sink.Count >= 4, "nested demand reached final Author read after Book12 key capture");
            scope.Detach(related);
            Check.That(!scope.State(related, BookOrm.Paths.Author).IsAttached, "related Book12 detached while read paused");
            pause.Release();
            var error = await Check.ThrowsAsync<RelationScopeException>(() => loading);
            Check.That(error.Error == RelationScopeError.Detached, "changed publication participant refuses atomically");
            Check.That(!scope.State(related, BookOrm.Paths.Author).IsAttached
                && ReferenceEquals(related.Author, original), "related Book12 stays detached with original navigation");
            Check.That(root.Editor is null, "nested refusal publishes no initiating navigation");
        }
        finally { pause.Release(); f.Sink.Pause = null; }
    }
}
