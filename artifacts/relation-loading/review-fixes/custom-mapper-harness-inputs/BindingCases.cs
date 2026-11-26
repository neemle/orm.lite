using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class BindingCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var unit = await UnitOfWork.BeginAsync(f.Context);
        await using var scope = LoadingScope.Bind(f.Context, unit, Fixture.Options);
        var options = new RelationReadOptions(Scope: scope);
        var author = await AuthorOrm.GetAsync(f.Context, unit.Connection, 1, options);
        Check.That(author?.Books.Count == 2, "generated connection+unit transaction bridge");
        var events = f.Sink.Statements;
        Check.That(events.Length == 3 && events.All(e => e.ConnectionId == events[0].ConnectionId
            && e.TransactionId == events[0].TransactionId && e.TransactionId is not null), "exact resource identity");
        var before = f.Sink.Count;
        var strict = await Check.ThrowsAsync<RelationScopeException>(() =>
            AuthorOrm.Query(f.Context).On(unit.Connection).FirstOrDefaultAsync(options));
        Check.That(strict.Error == RelationScopeError.DifferentScope && f.Sink.Count == before, "ordinary explicit null tx strict");
        var bridge = await Check.ThrowsAsync<RelationScopeException>(() =>
            RelationQueryExtensions.FirstOrDefaultAsync(AuthorOrm.Query(f.Context).On(unit.Connection),
                unit.Connection, options, default));
        Check.That(bridge.Error == RelationScopeError.DifferentScope && f.Sink.Count == before, "bridge explicit null tx strict");
        var same = await AuthorOrm.Query(f.Context).On(unit).FirstOrDefaultAsync(options);
        Check.That(same is not null, "exact explicit unit remains valid");
        await unit.CommitAsync();
        before = f.Sink.Count;
        await Check.ThrowsAsync<RelationScopeException>(() => scope.LoadAsync(author!, AuthorOrm.Paths.Tags));
        Check.That(f.Sink.Count == before, "commit invalidates demand before SQL");
        await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(f.Context, unit.Connection, null, Fixture.Options)));
        Check.That(f.Sink.Count == before, "known committed unit origin never downgrades to bare witness");
    }
}
