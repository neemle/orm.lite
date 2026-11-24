using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class BindOrderTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid Alpha = Guid.NewGuid();
    private static readonly Guid Bravo = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using (var create = _connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_widgets (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(Alpha, "alpha", 10);
        await SeedAsync(Bravo, "bravo", 20);

        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, new[] { "id", "name", "rank" });
        _ctx = TestContext.Of(new NeverUsedDataSource(), new SqliteDialect());
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact(DisplayName = "Where then SetCoalesce renders the SET parameter @p1 before the WHERE parameter @p0 and still updates the right row, because parameters bind by name and the list holds them in numbering order")]
    public async Task Where_then_SetCoalesce_is_correct_only_because_parameters_bind_by_name()
    {
        Assert.Equal(
            "UPDATE \"fixture_widgets\" SET \"name\" = COALESCE(@p1, \"name\") WHERE \"rank\" = @p0",
            Query().Where(w => w.Rank == 10).SetCoalesce(w => w.Name, "zulu").RenderUpdate());

        var updated = await Query().Where(w => w.Rank == 10).SetCoalesce(w => w.Name, "zulu").UpdateAsync();

        Assert.Equal(1, updated);
        Assert.Equal("zulu", await NameOfAsync(Alpha));
        Assert.Equal("bravo", await NameOfAsync(Bravo));
    }

    private async Task<string?> NameOfAsync(Guid id)
    {
        await using var read = _connection.CreateCommand();
        read.CommandText = "SELECT name FROM fixture_widgets WHERE id = $id";
        read.Parameters.AddWithValue("$id", id);
        return (string?)await read.ExecuteScalarAsync();
    }

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

    private Query<FixtureWidget> Query() =>
        new Query<FixtureWidget>(_ctx, Map).On(_connection);
}
