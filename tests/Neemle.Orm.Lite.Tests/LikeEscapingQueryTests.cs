using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class LikeEscapingQueryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid Plain = Guid.NewGuid();
    private static readonly Guid Percent = Guid.NewGuid();
    private static readonly Guid Underscore = Guid.NewGuid();
    private static readonly Guid Backslash = Guid.NewGuid();
    private static readonly Guid Tilde = Guid.NewGuid();
    private static readonly Guid Unrelated = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using (var create = _connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_widgets (id TEXT PRIMARY KEY, name TEXT NOT NULL, rank INTEGER NOT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(Plain, "battery", 1);
        await SeedAsync(Percent, "battery 100%", 2);
        await SeedAsync(Underscore, "agent_run", 3);
        await SeedAsync(Backslash, @"C:\temp", 4);
        await SeedAsync(Tilde, "approx ~50 items", 5);
        await SeedAsync(Unrelated, "kittens", 6);

        OrmMeta.Register<FixtureWidget>("fixture_widgets", Col, new[] { "id", "name", "rank" });
        _ctx = TestContext.Of(new NeverUsedDataSource(), new SqliteDialect());
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task A_percent_in_the_value_matches_the_row_containing_a_percent_not_every_row()
    {
        var rows = await Query().Where(w => w.Name.Contains("%")).ToListAsync();

        Assert.Equal(Percent, Assert.Single(rows).Id);
        Assert.Equal(1, await Query().Where(w => w.Name.Contains("%")).CountAsync());
    }

    [Fact]
    public async Task An_underscore_in_the_value_matches_only_the_row_containing_an_underscore()
    {
        var rows = await Query().Where(w => w.Name.Contains("_")).ToListAsync();

        Assert.Equal(Underscore, Assert.Single(rows).Id);
    }

    [Fact]
    public async Task A_backslash_in_the_value_is_matched_as_text_and_does_not_corrupt_the_pattern()
    {
        Assert.Equal(Backslash, Assert.Single(await Query().Where(w => w.Name.Contains(@"\")).ToListAsync()).Id);
        Assert.Equal(Backslash, Assert.Single(await Query().Where(w => w.Name.Contains(@"C:\temp")).ToListAsync()).Id);

        Assert.Empty(await Query().Where(w => w.Name.Contains(@"\%")).ToListAsync());
    }

    [Fact]
    public async Task A_tilde_in_the_value_is_matched_as_text_and_never_escapes_the_next_character()
    {
        Assert.Equal(Tilde, Assert.Single(await Query().Where(w => w.Name.Contains("~")).ToListAsync()).Id);
        Assert.Equal(Tilde, Assert.Single(await Query().Where(w => w.Name.Contains("approx ~50")).ToListAsync()).Id);

        Assert.Empty(await Query().Where(w => w.Name.Contains("~%")).ToListAsync());
    }

    [Fact]
    public async Task A_value_mixing_literals_with_wildcards_matches_only_the_literal_text()
    {
        Assert.Equal(Percent, Assert.Single(await Query().Where(w => w.Name.Contains("100%")).ToListAsync()).Id);
        Assert.Equal(Underscore, Assert.Single(await Query().Where(w => w.Name.Contains("agent_")).ToListAsync()).Id);

        Assert.Empty(await Query().Where(w => w.Name.Contains("batter_")).ToListAsync());
        Assert.Equal(2, await Query().Where(w => w.Name.Contains("batter")).CountAsync());
    }

    [Fact]
    public async Task Ordinary_substring_prefix_and_suffix_searches_are_unchanged()
    {
        Assert.Equal(2, await Query().Where(w => w.Name.Contains("battery")).CountAsync());
        Assert.Equal(2, await Query().Where(w => w.Name.StartsWith("batter")).CountAsync());
        Assert.Equal(Unrelated, Assert.Single(await Query().Where(w => w.Name.EndsWith("ttens")).ToListAsync()).Id);
        Assert.Equal(6, await Query().Where(w => w.Name.Contains("")).CountAsync());
    }

    [Fact]
    public async Task A_wildcard_only_prefix_matches_only_a_name_that_starts_with_that_character()
    {
        Assert.Empty(await Query().Where(w => w.Name.StartsWith("%")).ToListAsync());
        Assert.Empty(await Query().Where(w => w.Name.StartsWith("_")).ToListAsync());

        Assert.Empty(await Query().Where(w => w.Name.StartsWith("100%")).ToListAsync());
        Assert.Equal(Underscore, Assert.Single(await Query().Where(w => w.Name.StartsWith("agent_")).ToListAsync()).Id);
    }

    [Fact]
    public async Task A_wildcard_only_suffix_matches_only_a_name_that_ends_with_that_character()
    {
        Assert.Equal(Percent, Assert.Single(await Query().Where(w => w.Name.EndsWith("%")).ToListAsync()).Id);
        Assert.Empty(await Query().Where(w => w.Name.EndsWith("_")).ToListAsync());
    }

    [Fact]
    public async Task The_ESCAPE_clause_is_what_makes_the_escaped_pattern_mean_anything_to_SQLite()
    {
        Assert.Equal(Percent, Assert.Single(await Query().Where(w => w.Name.Contains("%")).ToListAsync()).Id);

        Assert.Equal("approx ~50 items", await ScalarNameAsync("SELECT name FROM fixture_widgets WHERE name LIKE '%~%%'"));

        Assert.Equal("battery 100%", await ScalarNameAsync("SELECT name FROM fixture_widgets WHERE name LIKE '%~%%' ESCAPE '~'"));
    }

    private async Task<string> ScalarNameAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "expected exactly one row, got none: " + sql);
        var name = reader.GetString(0);
        Assert.False(await reader.ReadAsync(), "expected exactly one row, got more: " + sql);
        return name;
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
