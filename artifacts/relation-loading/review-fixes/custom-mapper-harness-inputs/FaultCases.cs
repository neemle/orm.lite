using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class FaultCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var scope = await LoadingScope.OpenAsync(f.Context, Fixture.Options);
        var author = (await AuthorOrm.Query(f.Context).Where(a => a.Id == 1)
            .ToListAsync(new(EagerBehavior.Suppress, scope))).Single();
        Tag.FailMapping = true;
        MapSentinelException failure;
        try { failure = await Check.ThrowsAsync<MapSentinelException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags)); }
        finally { Tag.FailMapping = false; }
        Check.That(author.Tags.Count == 0 && scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.Faulted,
            "mapper fault has no publication");
        var before = f.Sink.Count;
        var repeated = await Check.ThrowsAsync<MapSentinelException>(() => scope.LoadAsync(author, AuthorOrm.Paths.Tags));
        Check.That(ReferenceEquals(failure, repeated) && f.Sink.Count == before, "failure cached without hidden retry");
        await scope.RetryAsync(author, AuthorOrm.Paths.Tags);
        Check.That(author.Tags.Count == 2 && f.Sink.Count == before + 2, "explicit retry executes once");
        Check.That(scope.State(author, AuthorOrm.Paths.Tags).State == RelationLoadState.LoadedValue, "retry loaded state");
        await Check.ThrowsAsync<RelationScopeException>(() => scope.RetryAsync(author, AuthorOrm.Paths.Tags));
    }
}
