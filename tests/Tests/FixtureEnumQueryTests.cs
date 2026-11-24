using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class FixtureEnumQueryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid JobPending1 = Guid.NewGuid();
    private static readonly Guid JobPending2 = Guid.NewGuid();
    private static readonly Guid JobDone = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using (var create = _connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_jobs (id TEXT PRIMARY KEY, name TEXT NOT NULL, status TEXT NOT NULL, priority TEXT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(JobPending1, "one", FixtureStatus.pending, FixturePriority.low);
        await SeedAsync(JobPending2, "two", FixtureStatus.pending, null);
        await SeedAsync(JobDone, "three", FixtureStatus.done, FixturePriority.high);

        OrmMeta.Register<FixtureJob>("fixture_jobs", Col, new[] { "id", "name", "status", "priority" });
        _ctx = TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task SeedAsync(Guid id, string name, FixtureStatus status, FixturePriority? priority)
    {
        await using var insert = _connection.CreateCommand();
        insert.CommandText = "INSERT INTO fixture_jobs (id, name, status, priority) VALUES ($id, $name, $status, $priority)";
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$status", status.ToString());
        insert.Parameters.AddWithValue("$priority", (object?)priority?.ToString() ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync();
    }

    private static string Col(string member) => member switch
    {
        nameof(FixtureJob.Id) => "id",
        nameof(FixtureJob.Name) => "name",
        nameof(FixtureJob.Status) => "status",
        nameof(FixtureJob.Priority) => "priority",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureJob member."),
    };

    private static FixtureJob Map(DbDataReader r) => new(
        r.GetGuid(0), r.GetString(1), Enum.Parse<FixtureStatus>(r.GetString(2)),
        r.IsDBNull(3) ? null : Enum.Parse<FixturePriority>(r.GetString(3)));

    private Query<FixtureJob> Query() => new Query<FixtureJob>(_ctx, Map).On(_connection);

    [Fact]
    public async Task Enum_literal_equality_binds_the_member_name_and_matches_the_text_column()
    {
        var rows = await Query().Where(j => j.Status == FixtureStatus.done).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(JobDone, row.Id);
        Assert.Equal(FixtureStatus.done, row.Status);
    }

    [Fact]
    public async Task Enum_literal_equality_matches_the_zero_valued_member_too()
    {
        var rows = await Query().Where(j => j.Status == FixtureStatus.pending).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(FixtureStatus.pending, r.Status));
    }

    [Fact]
    public async Task Enum_literal_equality_works_with_the_enum_on_the_left_side_too()
    {
        var rows = await Query().Where(j => FixtureStatus.done == j.Status).ToListAsync();

        Assert.Single(rows);
    }

    [Fact]
    public async Task Enum_literal_inequality_binds_the_name_and_excludes_only_that_member()
    {
        var rows = await Query().Where(j => j.Status != FixtureStatus.done).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Id == JobDone);
    }

    [Fact]
    public async Task A_nullable_enum_column_matches_a_literal_by_name()
    {
        var rows = await Query().Where(j => j.Priority == FixturePriority.high).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(JobDone, row.Id);
    }

    [Fact]
    public async Task A_nullable_enum_column_is_null_predicate_still_translates_to_IS_NULL()
    {
        var rows = await Query().Where(j => j.Priority == null).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(JobPending2, row.Id);
    }

    [Fact]
    public async Task Enum_literal_count_matches_by_name()
    {
        var count = await Query().Where(j => j.Status == FixtureStatus.done).CountAsync();

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task An_enum_supplied_as_a_variable_still_matches_after_the_fix()
    {
        var wanted = FixtureStatus.done;
        var rows = await Query().Where(j => j.Status == wanted).ToListAsync();

        Assert.Single(rows);
    }
}
