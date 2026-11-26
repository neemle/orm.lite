using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class LifetimeCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await ScopeDisposeAsync(f);
        await ExpiryAsync(f);
        await UnitDrainAsync(f);
    }

    private static async Task<Author> RootAsync(Fixture f, LoadingScope scope) =>
        (await AuthorOrm.Query(f.Context).Where(a => a.Id == 1)
            .ToListAsync(new(EagerBehavior.Suppress, scope))).Single();

    private static async Task ScopeDisposeAsync(Fixture f)
    {
        var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        f.Sink.Pause = pause;
        var load = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var closing = scope.DisposeAsync().AsTask();
        Check.That(!closing.IsCompleted, "scope disposal drains admitted work");
        pause.Release();
        await Check.ThrowsAsync<OperationCanceledException>(() => load);
        await closing.WaitAsync(TimeSpan.FromSeconds(10));
        f.Sink.Pause = null;
        Check.That(author.Tags.Count == 0 && !scope.State(author, AuthorOrm.Paths.Tags).IsAttached, "closed fence");
        Check.That((await PlainOrm.Query(f.Context).ToListAsync()).Count == 2, "scope never owns context");
    }

    private static async Task ExpiryAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context,
            Fixture.Options with { Lifetime = TimeSpan.FromMilliseconds(150) });
        var author = await RootAsync(f, scope);
        await Task.Delay(220);
        var before = f.Sink.Count;
        var expired = await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        Check.That(expired.Error == RelationScopeError.Expired && f.Sink.Count == before, "expiry never renews or executes");
        Check.That(!scope.State(author, AuthorOrm.Paths.Tags).IsAttached, "expired scope is detached");
    }

    private static async Task UnitDrainAsync(Fixture f)
    {
        await using var unit = await UnitOfWork.BeginAsync(f.Context);
        await using var scope = LoadingScope.Bind(f.Context, unit, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        f.Sink.Pause = pause;
        var load = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var commit = unit.CommitAsync();
        Check.That(!commit.IsCompleted, "unit commit waits for cancellation/drain");
        pause.Release();
        await Check.ThrowsAsync<OperationCanceledException>(() => load);
        await commit.WaitAsync(TimeSpan.FromSeconds(10));
        f.Sink.Pause = null;
        Check.That(unit.Committed && author.Tags.Count == 0, "unit commits only after fenced drain");
    }

    internal static async Task ContextDrainAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = await RootAsync(f, scope);
        using var pause = new ReadPause("policy_author_tags");
        f.Sink.Pause = pause;
        var load = Task.Run(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var closing = f.Context.DisposeAsync().AsTask();
        Check.That(!closing.IsCompleted, "context disposal drains policy operations");
        pause.Release();
        await Check.ThrowsAsync<OperationCanceledException>(() => load);
        await closing.WaitAsync(TimeSpan.FromSeconds(10));
        f.Sink.Pause = null;
        var before = f.Sink.Count;
        await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        Check.That(f.Sink.Count == before && author.Tags.Count == 0, "disposed context cannot publish/reopen");
    }
}
