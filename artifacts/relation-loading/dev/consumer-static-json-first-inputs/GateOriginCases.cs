using System.Data;
using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace PolicySmoke;

internal static class GateOriginCases
{
    internal static async Task VerifyAsync(Fixture f, string provider)
    {
        await using var connection = await f.Context.DataSource.OpenConnectionAsync();
        await using var selected = new OrmContext(f.Context.DataSource, f.Context.Provider);
        await using (var scope = LoadingScope.Bind(selected, connection, null, Fixture.Options))
        {
            var options = new RelationReadOptions(Scope: scope);
            Check.That((await AuthorOrm.Query(selected).FirstOrDefaultAsync(options))?.Books.Count == 2,
                "external gate lease binds across same source/provider contexts");
            var before = f.Sink.Count;
            var error = await Check.ThrowsAsync<RelationScopeException>(() =>
                AuthorOrm.Query(f.Context).FirstOrDefaultAsync(options));
            Check.That(error.Error == RelationScopeError.DifferentScope && f.Sink.Count == before,
                "selected scope context remains exact despite shared gate");
        }
        Check.That(connection.State == ConnectionState.Open, "borrowed scope never owns gate connection");
        await WrongProviderAsync(f, connection, provider);
        await ReleasedAsync(f, connection);
        await DifferentGateAsync(f, provider);
    }

    private static async Task WrongProviderAsync(Fixture f, DbConnection connection, string provider)
    {
        ISqlProvider other = provider == "postgres" ? new Neemle.OrmLite.Postgres.PostgresProvider()
            : new Neemle.OrmLite.Sqlite.SqliteProvider(TimeSpan.FromSeconds(2));
        await using var wrong = new OrmContext(f.Context.DataSource, other);
        var before = f.Sink.Count;
        var error = await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(wrong, connection, null, Fixture.Options)));
        Check.That(error.Error == RelationScopeError.DifferentScope && f.Sink.Count == before,
            "provider object mismatch no SQL");
    }

    private static async Task ReleasedAsync(Fixture f, DbConnection connection)
    {
        await connection.CloseAsync();
        var error = await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(f.Context, connection, null, Fixture.Options)));
        Check.That(error.Error == RelationScopeError.InvalidResource, "released gate lease stale");
        await Check.ThrowsAsync<InvalidOperationException>(() => connection.OpenAsync());
        error = await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(f.Context, connection, null, Fixture.Options)));
        Check.That(error.Error == RelationScopeError.InvalidResource, "reopened object cannot revive old witness");
    }

    private static async Task DifferentGateAsync(Fixture f, string provider)
    {
        await using var other = await Fixture.OpenAsync(provider);
        await using var connection = await other.Context.DataSource.OpenConnectionAsync();
        var before = f.Sink.Count + other.Sink.Count;
        var error = await Check.ThrowsAsync<RelationScopeException>(() => Task.FromResult(
            LoadingScope.Bind(f.Context, connection, null, Fixture.Options)));
        Check.That(error.Error == RelationScopeError.DifferentScope && f.Sink.Count + other.Sink.Count == before,
            "different observed gate refuses no SQL");
    }
}
