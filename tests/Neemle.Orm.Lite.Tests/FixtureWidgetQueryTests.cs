using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class FixtureWidgetQueryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid WidgetAlpha = Guid.NewGuid();
    private static readonly Guid WidgetBravo = Guid.NewGuid();
    private static readonly Guid WidgetCharlie = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using (var create = _connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_widgets (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(WidgetAlpha, "alpha", 10);
        await SeedAsync(WidgetBravo, "bravo", 20);
        await SeedAsync(WidgetCharlie, "charlie", 30);

        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, new[] { "id", "name", "rank" });
        _ctx = new OrmContext(new NeverUsedDataSource(), new SqliteDialect());
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

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

    private Query<FixtureWidget> Query() => new Query<FixtureWidget>(_ctx, Map).On(_connection);

    [Fact]
    public async Task Where_equality_predicate_returns_only_the_matching_row()
    {
        var rows = await Query().Where(w => w.Name == "bravo").ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(WidgetBravo, row.Id);
        Assert.Equal("bravo", row.Name);
        Assert.Equal(20, row.Rank);
    }

    [Fact]
    public async Task Where_range_predicate_returns_every_row_above_the_threshold()
    {
        var rows = await Query().Where(w => w.Rank > 10).ToListAsync();

        Assert.Equal(
            new HashSet<Guid> { WidgetBravo, WidgetCharlie },
            rows.Select(r => r.Id).ToHashSet());
    }

    [Fact]
    public async Task Where_combined_with_ToListAsync_returns_an_empty_list_when_nothing_matches()
    {
        var rows = await Query().Where(w => w.Rank > 1000).ToListAsync();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_returns_the_lowest_ranked_row_at_or_above_the_threshold()
    {
        var row = await Query().Where(w => w.Rank >= 20).OrderBy(w => w.Rank).FirstOrDefaultAsync();

        Assert.NotNull(row);
        Assert.Equal(WidgetBravo, row!.Id);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_returns_null_when_no_row_matches()
    {
        var row = await Query().Where(w => w.Name == "does-not-exist").FirstOrDefaultAsync();

        Assert.Null(row);
    }
}
