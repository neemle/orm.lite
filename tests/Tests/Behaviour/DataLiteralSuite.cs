using System.Globalization;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class DataLiteralSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private const string RowIds = "SELECT id FROM dl_delete ORDER BY id";

    private const string RowIdsText = "SELECT id::text FROM dl_delete ORDER BY id";

    private static readonly Guid RowA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");

    private static readonly Guid RowB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    private static readonly Guid RowC = Guid.Parse("c0000000-0000-0000-0000-00000000000c");

    private static readonly string[] KindColumns =
        ["id", "stamp", "day", "mood", "initial", "amount", "ratio", "flag", "note", "doc"];

    private static readonly object?[] KindsFirst =
    [
        RowA, new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc), new DateOnly(2024, 2, 29), Mood.Upset,
        'x', 2.25m, 2.5, true, null, Jsonb.Parse("{\"k\":\"it's\"}"),
    ];

    private static readonly object?[] KindsSecond =
    [
        RowB, new DateTimeOffset(2024, 1, 2, 10, 0, 0, new TimeSpan(5, 30, 0)), new DateOnly(2024, 12, 31),
        Mood.Calm, '\'', 1.5m, 0.25, false, "it's", Jsonb.Parse("{\"k\":\"a'b\\\"c\"}"),
    ];

    protected TFixture Fixture { get; } = fixture;

    private OrmContext Ctx => Fixture.Context;

    private bool Postgres => Fixture.ProviderId == "postgres";

    private enum Mood
    {
        Calm,
        Upset,
    }

    private static string Cell(object? value) =>
        value is null ? "~" : Convert.ToString(value, CultureInfo.InvariantCulture)!;

    private Task RunAsync(string version, MigrationBuilder builder) =>
        MigrationRunner.RunAsync(
            Ctx.DataSource,
            Ctx.Provider,
            [MigrationContribution.Of(version, Reversibility.Irreversible, builder.Nodes)]);

    private async Task<string> RowsAsync(string sqlite, string postgres)
    {
        var rows = await Fixture.RowsAsync(Postgres ? postgres : sqlite);
        return string.Join(" ; ", rows.Select(row => string.Join("|", row.Select(Cell))));
    }

    private Task<string> IdsAsync() => RowsAsync(RowIds, RowIdsText);

    private MigrationBuilder KindsMigration() => new MigrationBuilder(Ctx.Provider)
        .CreateTable("dl_kinds", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("stamp", ColumnType.Timestamp)
            .Column("day", ColumnType.Date)
            .Column("mood", ColumnType.Text)
            .Column("initial", ColumnType.Text)
            .DecimalColumn("amount", 12, 4)
            .Column("ratio", ColumnType.Double)
            .Column("flag", ColumnType.Bool)
            .Column("note", ColumnType.Text)
            .Column("doc", ColumnType.Json))
        .InsertData("dl_kinds", KindColumns, KindsFirst, KindsSecond);

    private string ExpectedKinds() => Postgres
        ? "a0000000-0000-0000-0000-00000000000a|2024-01-02T10:00:00.000000|2024-02-29|Upset|x|2.25|2.5|true|~|it's ; "
            + "b0000000-0000-0000-0000-00000000000b|2024-01-02T04:30:00.000000|2024-12-31|Calm|'|1.5|0.25|false"
            + "|it's|a'b\"c"
        : "a0000000-0000-0000-0000-00000000000a|2024-01-02T10:00:00.0000000Z|2024-02-29|Upset|x|2.25|2.5|1|~|it's ; "
            + "b0000000-0000-0000-0000-00000000000b|2024-01-02T04:30:00.0000000Z|2024-12-31|Calm|'|1.5|0.25|0"
            + "|it's|a'b\"c";

    private Task<string> KindsAsync() => RowsAsync(
        "SELECT id, stamp, day, mood, initial, amount, ratio, flag, note, json_extract(doc, '$.k') "
        + "FROM dl_kinds ORDER BY id",
        "SELECT id::text, to_char(stamp AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US'), "
        + "day::text, mood, initial, trim_scale(amount)::text, ratio::text, flag::text, note, "
        + "doc->>'k' FROM dl_kinds ORDER BY id");

    [SkippableFact(DisplayName = "a migration built with InsertData writes a Guid, a moment, a day, an enum, a char, "
        + "a decimal, a double, a flag, a null and a document into a real table, and the rows read back as written")]
    public async Task Literals_of_every_kind_are_written_and_read_back()
    {
        Fixture.RequireAvailable();

        await RunAsync("1600_dl_kinds", KindsMigration());

        Assert.Equal(ExpectedKinds(), await KindsAsync());
    }

    [SkippableFact(DisplayName = "a DeleteData by Guid removes exactly that row, and a DeleteData by enum name "
        + "removes exactly the rows with that member")]
    public async Task A_delete_by_guid_and_by_enum_name_removes_only_those_rows()
    {
        Fixture.RequireAvailable();
        var seed = new MigrationBuilder(Ctx.Provider)
            .CreateTable("dl_delete", t => t.Column("id", ColumnType.Uuid, false, true).Column("mood", ColumnType.Text))
            .InsertData("dl_delete", ["id", "mood"], [RowA, Mood.Calm], [RowB, Mood.Upset], [RowC, Mood.Calm]);
        await RunAsync("1601_dl_delete_seed", seed);

        var byGuid = new MigrationBuilder(Ctx.Provider).DeleteData("dl_delete", "id", RowA);
        await RunAsync("1602_dl_delete_by_guid", byGuid);
        var afterGuid = await IdsAsync();
        var byEnum = new MigrationBuilder(Ctx.Provider).DeleteData("dl_delete", "mood", Mood.Upset);
        await RunAsync("1603_dl_delete_by_enum", byEnum);
        var afterEnum = await IdsAsync();

        Assert.Equal("b0000000-0000-0000-0000-00000000000b ; c0000000-0000-0000-0000-00000000000c", afterGuid);
        Assert.Equal("c0000000-0000-0000-0000-00000000000c", afterEnum);
    }

    [SkippableFact(DisplayName = "[SEC] a string that tries to end its literal and drop its table is stored as "
        + "plain text, and the table is still there")]
    public async Task A_hostile_string_is_stored_as_text()
    {
        Fixture.RequireAvailable();
        const string hostile = "x'); DROP TABLE dl_probe; --";
        var builder = new MigrationBuilder(Ctx.Provider)
            .CreateTable("dl_probe", t => t.Column("id", ColumnType.Text, false, true).Column("v", ColumnType.Text))
            .InsertData("dl_probe", ["id", "v"], ["h1", hostile], ["h2", "semi; -- not a comment"]);
        await RunAsync("1604_dl_probe", builder);

        const string read = "SELECT id, v FROM dl_probe ORDER BY id";
        Assert.Equal("h1|" + hostile + " ; h2|semi; -- not a comment", await RowsAsync(read, read));
    }
}
