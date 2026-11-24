using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class CountParityQueryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid Alpha = Guid.NewGuid();
    private static readonly Guid Bravo = Guid.NewGuid();
    private static readonly Guid Charlie = Guid.NewGuid();

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
        await SeedAsync(Charlie, "charlie", 30);

        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, new[] { "id", "name", "rank" });
        _ctx = TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task CountAsync_applies_every_appended_Where_clause_not_only_the_first()
    {
        Assert.Equal(2, await Query().Where(w => w.Rank > 10).CountAsync());

        var both = await Query().Where(w => w.Rank > 10).Where(w => w.Rank < 30).CountAsync();
        Assert.Equal(1, both);

        var rows = await Query().Where(w => w.Rank > 10).Where(w => w.Rank < 30).ToListAsync();
        Assert.Equal(both, rows.Count);
        Assert.Equal(Bravo, Assert.Single(rows).Id);
    }

    [Fact]
    public async Task Paging_the_page_chain_does_not_cap_the_count_chain()
    {
        var total = await Query().Where(w => w.Rank > 0).CountAsync();
        var page = await Query().Where(w => w.Rank > 0).OrderBy(w => w.Rank).Take(1).ToListAsync();

        Assert.Equal(3, total);
        Assert.Single(page);
        Assert.Equal(Alpha, page[0].Id);
    }

    [Fact]
    public async Task An_empty_NOT_IN_matches_every_row_while_an_empty_IN_matches_none()
    {
        var populated = new List<string> { "bravo", "charlie" };
        Assert.Equal(1, await Query().Where(w => !populated.Contains(w.Name)).CountAsync());

        var empty = new List<string>();
        Assert.Equal(3, await Query().Where(w => !empty.Contains(w.Name)).CountAsync());
        Assert.Equal(0, await Query().Where(w => empty.Contains(w.Name)).CountAsync());
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
