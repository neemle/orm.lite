using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class SqliteDataLiteralTests(SqliteFixture fixture) : DataLiteralSuite<SqliteFixture>(fixture)
{
    private static readonly (string Column, ColumnType Type, object Value)[] Kinds =
    [
        ("c_guid", ColumnType.Uuid, Guid.Parse("a0000000-0000-0000-0000-00000000000a")),
        ("c_utc", ColumnType.Timestamp, new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567)),
        ("c_unspecified", ColumnType.Timestamp, new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Unspecified)),
        ("c_local", ColumnType.Timestamp, new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Local)),
        ("c_shifted", ColumnType.Timestamp, new DateTimeOffset(2024, 1, 2, 10, 0, 0, new TimeSpan(5, 30, 0))),
        ("c_day", ColumnType.Date, new DateOnly(2024, 2, 29)),
        ("c_mood", ColumnType.Text, Mood.Calm),
        ("c_doc", ColumnType.Json, Jsonb.Parse("{\"k\":\"it's\"}")),
        ("c_char", ColumnType.Text, 'x'),
    ];

    private enum Mood
    {
        Calm,
    }

    private async Task<long> FoundAsync(string column, object value)
    {
        await using var connection = await Fixture.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM dl_bound WHERE {column} = @p";
        command.AddParam("p", SqliteValueCodec.Encode(value));
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<string?> StoredAsync(string column)
    {
        var rows = await Fixture.RowsAsync($"SELECT {column} FROM dl_bound");
        return Convert.ToString(Assert.Single(rows)[0], CultureInfo.InvariantCulture);
    }

    [SkippableFact(DisplayName = "a value written as a literal by InsertData is the row a WHERE bound with the SQLite "
        + "parameter text finds, for an id, a moment of every kind, an offset moment, a day, an "
        + "enum, a document and a char")]
    public async Task A_written_literal_equals_the_bound_parameter_text()
    {
        Fixture.RequireAvailable();
        var builder = new MigrationBuilder(Fixture.Context.Provider)
            .CreateTable("dl_bound", t =>
            {
                foreach (var kind in Kinds) t.Column(kind.Column, kind.Type);
            })
            .InsertData("dl_bound", Kinds.Select(k => k.Column).ToArray(), Kinds.Select(k => k.Value).ToArray());
        await MigrationRunner.RunAsync(
            Fixture.Context.DataSource,
            Fixture.Context.Provider,
            [MigrationContribution.Of("1610_dl_bound", Reversibility.Irreversible, builder.Nodes)]);

        foreach (var kind in Kinds) Assert.Equal(1L, await FoundAsync(kind.Column, kind.Value));
        foreach (var kind in Kinds.Where(k => k.Value is Guid or DateTime or DateTimeOffset))
            Assert.Equal(SqliteValueCodec.Encode(kind.Value), await StoredAsync(kind.Column));
    }
}
