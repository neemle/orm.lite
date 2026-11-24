using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class SqliteDecimalLiteralTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly decimal[] Amounts =
    [
        1.10m, 0.30m, 2.25m, 123456789012.12345678m, 12345678901234567.89m,
    ];

    private SqliteFixture Fixture { get; } = fixture;

    private static object?[][] Rows() => Amounts.Select((a, i) => new object?[] { i + 1, a }).ToArray();

    private static string[] Expected() =>
        Amounts.Select(a => a.ToString(CultureInfo.InvariantCulture)).ToArray();

    private static MigrationBuilder TableOf(MigrationBuilder builder, string name) => builder
        .CreateTable(name, t => t.Column("id", ColumnType.Int32, false, true).DecimalColumn("amount", 28, 8));

    private Task RunAsync(string version, MigrationBuilder builder) =>
        MigrationRunner.RunAsync(
            Fixture.Context.DataSource,
            Fixture.Context.Provider,
            [MigrationContribution.Of(version, Reversibility.Irreversible, builder.Nodes)]);

    private async Task<string[]> AmountsAsync(string table)
    {
        var rows = await Fixture.RowsAsync($"SELECT amount FROM {table} ORDER BY id");
        return rows.Select(r => Convert.ToString(r[0], CultureInfo.InvariantCulture)!).ToArray();
    }

    [SkippableFact(DisplayName = "a decimal written as a literal by InsertData is stored with its scale and every "
        + "digit, the same text a bound parameter stores")]
    public async Task A_written_decimal_equals_the_bound_parameter_text()
    {
        Fixture.RequireAvailable();
        var provider = Fixture.Context.Provider;
        await RunAsync(
            "1710_dl_decimal",
            TableOf(new MigrationBuilder(provider), "dl_decimal").InsertData("dl_decimal", ["id", "amount"], Rows()));
        await RunAsync("1711_dl_decimal_bound", TableOf(new MigrationBuilder(provider), "dl_bound_dec"));
        foreach (var row in Rows()) await InsertBoundAsync((int)row[0]!, (decimal)row[1]!);

        Assert.Equal(Expected(), await AmountsAsync("dl_decimal"));
        Assert.Equal(await AmountsAsync("dl_bound_dec"), await AmountsAsync("dl_decimal"));
    }

    private async Task InsertBoundAsync(int id, decimal amount)
    {
        await using var connection = await Fixture.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dl_bound_dec (id, amount) VALUES (@id, @amount)";
        command.AddParam("id", id);
        command.AddParam("amount", SqliteValueCodec.Encode(amount));
        await command.ExecuteNonQueryAsync();
    }

    [SkippableFact(DisplayName = "a DeleteData by a decimal removes exactly the row with that value, also when the "
        + "value has more than fifteen significant digits or a trailing zero")]
    public async Task A_delete_by_decimal_matches_the_exact_value()
    {
        Fixture.RequireAvailable();
        var provider = Fixture.Context.Provider;
        await RunAsync(
            "1712_dl_decimal_delete_seed",
            TableOf(new MigrationBuilder(provider), "dl_del_dec").InsertData("dl_del_dec", ["id", "amount"], Rows()));

        await RunAsync(
            "1713_dl_decimal_delete",
            new MigrationBuilder(provider).DeleteData("dl_del_dec", "amount", 12345678901234567.89m, 1.10m));

        Assert.Equal(["0.30", "2.25", "123456789012.12345678"], await AmountsAsync("dl_del_dec"));
    }
}
