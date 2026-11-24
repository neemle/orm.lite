using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Microsoft.Data.Sqlite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class EqualsIgnoreCaseTests : IAsyncLifetime
{
    private static readonly Guid Lower = Guid.NewGuid();
    private static readonly Guid Mixed = Guid.NewGuid();
    private static readonly Guid Wildcard = Guid.NewGuid();

    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await using (var create = _connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_widgets (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(Lower, "a@example.test", 1);
        await SeedAsync(Mixed, "Bob@Acme.test", 2);
        await SeedAsync(Wildcard, "ab@example.test", 3);

        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, new[] { "id", "name", "rank" });
        _ctx = TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact(DisplayName = "Db.EqualsIgnoreCase renders lower(column) = lower(parameter) with no dialect member, for the Postgres and the SQLite dialect alike")]
    public void Renders_lower_on_both_sides()
    {
        var value = "A@Example.test";
        const string expected = "DELETE FROM \"fixture_widgets\" WHERE lower(\"name\") = lower(@p0)";

        Assert.Equal(expected, Detached(PostgresProvider.Instance).Where(w => Db.EqualsIgnoreCase(w.Name, value)).RenderDelete());
        Assert.Equal(expected, Detached(FixtureProvider.Instance).Where(w => Db.EqualsIgnoreCase(w.Name, value)).RenderDelete());
    }

    [Fact(DisplayName = "SANITIZATION PROBE — Db.EqualsIgnoreCase binds its value: a SQL fragment appears nowhere in the statement")]
    public void Never_renders_its_value()
    {
        var hostile = "x\") OR 1=1 --";

        var sql = Detached(PostgresProvider.Instance).Where(w => Db.EqualsIgnoreCase(w.Name, hostile)).RenderDelete();

        Assert.Equal("DELETE FROM \"fixture_widgets\" WHERE lower(\"name\") = lower(@p0)", sql);
        Assert.DoesNotContain("OR 1=1", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Db.EqualsIgnoreCase refuses a first argument that is not a column of the queried entity")]
    public void Refuses_a_non_column_first_argument()
    {
        var literal = "a@example.test";

        var refusal = Assert.Throws<NotSupportedException>(() =>
            Detached(PostgresProvider.Instance).Where(w => Db.EqualsIgnoreCase(literal, w.Name)).RenderDelete());

        Assert.Contains("Db.EqualsIgnoreCase", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Db.EqualsIgnoreCase is a query marker: calling it directly throws")]
    public void Throws_when_executed_directly()
    {
        Assert.Throws<InvalidOperationException>(() => Db.EqualsIgnoreCase("a", "b"));
    }

    [Fact(DisplayName = "Db.EqualsIgnoreCase matches the one row whatever the case on either side, and a wildcard character in the value is literal")]
    public async Task Matches_across_case_and_treats_wildcards_as_text()
    {
        Assert.Equal(Lower, Assert.Single(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "A@EXAMPLE.TEST")).ToListAsync()).Id);
        Assert.Equal(Mixed, Assert.Single(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "bob@acme.test")).ToListAsync()).Id);
        Assert.Equal(Mixed, Assert.Single(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "BOB@ACME.TEST")).ToListAsync()).Id);

        Assert.Empty(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "a_@example.test")).ToListAsync());
        Assert.Empty(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "%@example.test")).ToListAsync());
        Assert.Empty(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, "absent@example.test")).ToListAsync());
    }

    [Fact(DisplayName = "Db.EqualsIgnoreCase with a null value matches no row")]
    public async Task A_null_value_matches_nothing()
    {
        string? none = null;

        Assert.Empty(await Query().Where(w => Db.EqualsIgnoreCase(w.Name, none)).ToListAsync());
    }

    private static Query<FixtureWidget> Detached(ISqlProvider provider) =>
        new(TestContext.Of(new NeverUsedDataSource(), provider), _ => throw new NotSupportedException());

    private Query<FixtureWidget> Query() => new Query<FixtureWidget>(_ctx, Map).On(_connection);

    private async Task SeedAsync(Guid id, string name, int rank)
    {
        await using var insert = _connection.CreateCommand();
        insert.CommandText = "INSERT INTO fixture_widgets (id, name, rank) VALUES ($id, $name, $rank)";
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$rank", rank);
        await insert.ExecuteNonQueryAsync();
    }

    private static string Col(string member) => member switch
    {
        nameof(FixtureWidget.Id) => "id",
        nameof(FixtureWidget.Name) => "name",
        nameof(FixtureWidget.Rank) => "rank",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureWidget member."),
    };

    private static FixtureWidget Map(DbDataReader r) => new(r.GetGuid(0), r.GetString(1), r.GetInt32(2));
}
