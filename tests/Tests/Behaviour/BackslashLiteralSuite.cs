using System.Globalization;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class BackslashLiteralSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly string[] Samples =
    [
        @"a\b",
        @"\",
        @"end\",
        @"\'",
        @"'\",
        @"x\'); DROP TABLE bs_text; --",
        @"\\",
        @"C:\dir\file",
        @"{""k"":""a\""b\n""}",
    ];

    protected TFixture Fixture { get; } = fixture;

    protected static object?[][] Rows() => Samples
        .Select((text, index) => new object?[] { "r" + index, text })
        .Append(new object?[] { "rc", '\\' })
        .ToArray();

    protected static string[] Expected() => Samples
        .Select((text, index) => "r" + index + "|" + text)
        .Append(@"rc|\")
        .ToArray();

    protected static MigrationBuilder TableOf(MigrationBuilder builder, string name) => builder
        .CreateTable(name, t => t.Column("id", ColumnType.Text, false, true).Column("v", ColumnType.Text));

    protected Task RunAsync(string version, MigrationBuilder builder) =>
        MigrationRunner.RunAsync(
            Fixture.Context.DataSource,
            Fixture.Context.Provider,
            [MigrationContribution.Of(version, Reversibility.Irreversible, builder.Nodes)]);

    protected async Task<string[]> ReadAsync(string table)
    {
        var rows = await Fixture.RowsAsync($"SELECT id, v FROM {table} ORDER BY id");
        return rows
            .Select(r => Convert.ToString(r[0], CultureInfo.InvariantCulture) + "|"
                + Convert.ToString(r[1], CultureInfo.InvariantCulture))
            .ToArray();
    }

    [SkippableFact(DisplayName = "[SEC] strings with a backslash alone, trailing, before a quote, doubled, in a path "
        + "and in a JSON escape, and a backslash char, are stored and read back exactly as written, and the table "
        + "named in a hostile string survives")]
    public async Task Backslash_text_is_stored_as_written()
    {
        Fixture.RequireAvailable();
        var builder = TableOf(new MigrationBuilder(Fixture.Context.Provider), "bs_text")
            .InsertData("bs_text", ["id", "v"], Rows());

        await RunAsync("1700_bs_text", builder);

        Assert.Equal(Expected(), await ReadAsync("bs_text"));
    }
}
