using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class CustomOriginCases
{
    internal static async Task VerifyAsync(Fixture f)
    {
        await using var source = new ObservedSource(f.Context.DataSource);
        await using var context = new OrmContext(source, f.Context.Provider);
        await using (var scope = await LoadingScope.OpenAsync(context, Fixture.Options))
        {
            var author = await AuthorOrm.Query(context).FirstOrDefaultAsync(new(Scope: scope));
            Check.That(author?.Books.Count == 2 && source.Opens == 1, "known custom Open origin no extra open");
        }
        await using (var unit = await UnitOfWork.BeginAsync(context))
        {
            await using var scope = LoadingScope.Bind(context, unit, Fixture.Options);
            Check.That((await AuthorOrm.GetAsync(context, unit.Connection, 1, new(Scope: scope)))?.Books.Count == 2
                && source.Opens == 2, "known custom unit exact transaction no extra open");
        }
        await using var connection = await source.OpenConnectionAsync();
        var before = f.Sink.Count;
        var error = await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(context, connection, null, Fixture.Options)));
        Check.That(error.Error == RelationScopeError.InvalidResource && source.Opens == 3 && f.Sink.Count == before,
            "unobserved custom external origin refuses without extra IO");
    }
}
