using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class ConcurrencyCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await JoinedCancellationAsync(f);
        await InitiatingCancellationAsync(f);
        await DetachAsync(f);
    }

    private static async Task<Author> RootAsync(Fixture f, LoadingScope scope) =>
        (await AuthorOrm.Query(f.Context).Where(a => a.Id == 1)
            .ToListAsync(new(EagerBehavior.Suppress, scope))).Single();

    private static async Task JoinedCancellationAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        using var joinCancellation = new CancellationTokenSource();
        f.Sink.Pause = pause;
        var main = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check.That(scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.Loading, "in-flight state visible");
        var join = scope.LoadAsync(author, AuthorOrm.Paths.Tags, joinCancellation.Token);
        var another = scope.LoadAsync(author, AuthorOrm.Paths.Tags);
        joinCancellation.Cancel();
        await Check.ThrowsAsync<OperationCanceledException>(() => join);
        Check.That(!main.IsCompleted && author.Tags.Count == 0, "joining cancel leaves operation active and unpublished");
        var before = f.Sink.Count;
        pause.Release();
        await Task.WhenAll(main, another).WaitAsync(TimeSpan.FromSeconds(10));
        f.Sink.Pause = null;
        Check.That(author.Tags.Count == 2 && f.Sink.Count == before + 1, "one joined load and single publication");
    }

    private static async Task InitiatingCancellationAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        using var initiating = new CancellationTokenSource();
        f.Sink.Pause = pause;
        var main = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags, initiating.Token));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var join = scope.LoadAsync(author, AuthorOrm.Paths.Tags);
        initiating.Cancel();
        pause.Release();
        await Check.ThrowsAsync<OperationCanceledException>(() => main);
        await Check.ThrowsAsync<OperationCanceledException>(() => join);
        f.Sink.Pause = null;
        Check.That(author.Tags.Count == 0 && scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.Cancelled,
            "initiator cancels joined work without publication");
        var before = f.Sink.Count;
        await Check.ThrowsAsync<OperationCanceledException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        Check.That(f.Sink.Count == before, "cancelled result does not auto retry");
        await scope.RetryAsync(author, AuthorOrm.Paths.Tags);
        Check.That(author.Tags.Count == 2, "cancelled work can explicitly retry");
    }

    private static async Task DetachAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        f.Sink.Pause = pause;
        var main = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        scope.Detach(author);
        pause.Release();
        await Check.ThrowsAsync<OperationCanceledException>(() => main);
        f.Sink.Pause = null;
        Check.That(author.Tags.Count == 0 && !scope.State(author, AuthorOrm.Paths.Tags).IsAttached,
            "detach prevents late assignment");
    }
}
