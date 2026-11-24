using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class EnumReadAndInListTests : IAsyncLifetime
{
    private sealed record EnumJob(Guid Id, string Name, FixtureStatus Status, FixturePriority? Priority);

    private SqliteConnection _connection = null!;
    private OrmContext _ctx = null!;

    private static readonly Guid Low = Guid.NewGuid();
    private static readonly Guid HighA = Guid.NewGuid();
    private static readonly Guid HighB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        await using (var create = _connection.CreateCommand())
        {
            create.CommandText =
                "CREATE TABLE enum_jobs (id TEXT PRIMARY KEY, name TEXT NOT NULL, status TEXT NOT NULL, priority TEXT NULL)";
            await create.ExecuteNonQueryAsync();
        }

        await SeedAsync(Low, "low one", "pending", "low");
        await SeedAsync(HighA, "high one", "pending", "high");
        await SeedAsync(HighB, "high two", "done", "high");

        OrmMeta.Register<EnumJob>("enum_jobs", Col, ["id", "name", "status", "priority"]);
        _ctx = TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private async Task SeedAsync(Guid id, string name, string status, string? priority)
    {
        await using var insert = _connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO enum_jobs (id, name, status, priority) VALUES ($id, $name, $status, $priority)";
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$status", status);
        insert.Parameters.AddWithValue("$priority", (object?)priority ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync();
    }

    private static string Col(string member) => member switch
    {
        nameof(EnumJob.Id) => "id",
        nameof(EnumJob.Name) => "name",
        nameof(EnumJob.Status) => "status",
        nameof(EnumJob.Priority) => "priority",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown EnumJob member."),
    };

    private static EnumJob Map(DbDataReader r) => new(
        r.GetGuid(0), r.GetString(1), Enum.Parse<FixtureStatus>(r.GetString(2)),
        r.IsDBNull(3) ? null : Enum.Parse<FixturePriority>(r.GetString(3)));

    private Query<EnumJob> Query() => new Query<EnumJob>(_ctx, Map).On(_connection);



    [Fact]
    public async Task An_IN_over_a_list_of_enums_binds_each_member_by_name_and_matches_the_text_column()
    {
        var wanted = new List<FixtureStatus> { FixtureStatus.done };

        var rows = await Query().Where(j => wanted.Contains(j.Status)).ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal(HighB, row.Id);
    }

    [Fact]
    public async Task An_IN_over_several_enum_members_matches_all_of_them()
    {
        var wanted = new List<FixtureStatus> { FixtureStatus.pending, FixtureStatus.done };

        var rows = await Query().Where(j => wanted.Contains(j.Status)).ToListAsync();

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public async Task A_negated_IN_over_enums_excludes_exactly_those_members()
    {
        var unwanted = new List<FixtureStatus> { FixtureStatus.done };

        var rows = await Query().Where(j => !unwanted.Contains(j.Status)).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Id == HighB);
    }

    [Fact]
    public async Task An_IN_over_enums_counts_the_same_rows_it_lists()
    {
        var wanted = new List<FixtureStatus> { FixtureStatus.pending };

        var count = await Query().Where(j => wanted.Contains(j.Status)).CountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task An_IN_over_enums_binds_the_NAME_and_so_does_not_match_a_row_holding_the_ordinal()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET status = '1' WHERE name = 'high two'";
            await update.ExecuteNonQueryAsync();
        }
        var wanted = new List<FixtureStatus> { FixtureStatus.done };

        var rows = await Query().Where(j => wanted.Contains(j.Status)).ToListAsync();

        Assert.Empty(rows);
    }


    private async Task<DbDataReader> ReadOneAsync(string sql)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return reader;
    }

    [Fact]
    public async Task A_stored_value_in_another_case_still_reads_back_as_its_member()
    {
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        Assert.Equal(FixturePriority.high, OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));
    }

    [Fact]
    public async Task A_case_variant_that_Enum_Parse_would_have_thrown_on_now_reads()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = 'HIGH' WHERE name = 'high one'";
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        Assert.Equal(FixturePriority.high, OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));
        Assert.Throws<ArgumentException>(() => Enum.Parse<FixturePriority>("HIGH"));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("1")]
    [InlineData("low,high")]
    [InlineData("low, high")]
    public async Task A_value_that_is_not_a_member_NAME_is_refused_even_where_Enum_Parse_accepts_it(string stored)
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = $p WHERE name = 'high one'";
            update.Parameters.AddWithValue("$p", stored);
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        Assert.True(Enum.TryParse<FixturePriority>(stored, ignoreCase: true, out _));

        Assert.Throws<OrmEnumValueException>(
            () => OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));
    }

    [Fact]
    public async Task An_off_vocabulary_value_is_refused_naming_the_table_column_row_and_value()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = 'URGENT' WHERE name = 'high one'";
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync(
            "SELECT id, priority FROM enum_jobs WHERE name = 'high one'");

        var ex = Assert.Throws<OrmEnumValueException>(
            () => OrmEnum.Read<FixturePriority>(r, 1, "enum_jobs", "priority", "id"));

        Assert.Equal("enum_jobs", ex.Table);
        Assert.Equal("priority", ex.Column);
        Assert.Equal("URGENT", ex.Value);
        Assert.Equal(HighA.ToString(), ex.RowKey, ignoreCase: true);

        Assert.Contains("enum_jobs.priority", ex.Message, StringComparison.Ordinal);
        Assert.Contains(HighA.ToString(), ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"URGENT\"", ex.Message, StringComparison.Ordinal);
        Assert.Contains("low, high", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_string_is_refused_rather_than_read_as_the_zero_member()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = '' WHERE name = 'high one'";
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        Assert.Throws<OrmEnumValueException>(
            () => OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));
    }

    [Fact]
    public async Task Losing_the_key_column_still_produces_the_diagnosis_rather_than_a_different_error()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = 'URGENT' WHERE name = 'high one'";
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        var ex = Assert.Throws<OrmEnumValueException>(
            () => OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));

        Assert.Equal("", ex.RowKey);
        Assert.Equal("URGENT", ex.Value);
        Assert.DoesNotContain(" on row ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_surrounding_space_does_not_make_a_stored_value_unreadable()
    {
        await using (var update = _connection.CreateCommand())
        {
            update.CommandText = "UPDATE enum_jobs SET priority = ' LOW ' WHERE name = 'high one'";
            await update.ExecuteNonQueryAsync();
        }
        await using var r = await ReadOneAsync("SELECT priority FROM enum_jobs WHERE name = 'high one'");

        Assert.Equal(FixturePriority.low, OrmEnum.Read<FixturePriority>(r, 0, "enum_jobs", "priority", "id"));
    }

    [Fact]
    public void The_write_side_and_the_read_side_agree_on_exactly_which_strings_are_members()
    {
        foreach (var accepted in new[] { "low", "LOW", "Low", " high ", "high" })
            Assert.True(OrmEnum.TryParseExact<FixturePriority>(accepted, out _), accepted);

        foreach (var refused in new[] { "", "  ", "URGENT", "7", "1", "low,high", "lo" })
            Assert.False(OrmEnum.TryParseExact<FixturePriority>(refused, out _), refused);
    }
}
