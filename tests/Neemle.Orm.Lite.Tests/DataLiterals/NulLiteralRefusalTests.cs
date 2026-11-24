using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.DataLiterals;

public sealed class NulLiteralRefusalTests
{
    private const string Secret = "secret-text";

    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    public static IEnumerable<object[]> Carriers() =>
    [
        ["a\0b"],
        ["\0"],
        ["trailing\0"],
        ['\0'],
        [Jsonb.FromStorage("{\"k\":\"a\0b\"}")],
    ];

    public static IEnumerable<object[]> LoneSurrogates() =>
    [
        ["a\uD83Db"],
        ["a\uDE00b"],
        ["\uDE00\uD83D"],
        ["end\uD83D"],
        ['\uD83D'],
        ['\uDE00'],
    ];

    private static MigrationBuilder Busy(ISqlProvider provider) => new MigrationBuilder(provider).DropTable("earlier");

    [Theory(DisplayName = "[SEC] a string, a char or a document holding U+0000 is refused by InsertData and "
        + "DeleteData on both providers, naming U+0000, and nothing is recorded")]
    [MemberData(nameof(Carriers))]
    public void A_nul_is_refused_by_name(object value)
    {
        foreach (var provider in Providers)
        {
            var insert = Busy(provider);
            var inserted = Assert.Throws<ArgumentException>(() => insert.InsertData("t", ["c"], [value]));
            var delete = Busy(provider);
            var deleted = Assert.Throws<ArgumentException>(() => delete.DeleteData("t", "c", value));

            Assert.Contains("U+0000", inserted.Message, StringComparison.Ordinal);
            Assert.Contains("U+0000", deleted.Message, StringComparison.Ordinal);
            Assert.Equal("value", inserted.ParamName);
            Assert.Single(insert.Nodes);
            Assert.Single(delete.Nodes);
        }
    }

    [Theory(DisplayName = "[SEC] a string or char holding half of a surrogate pair is refused by InsertData and "
        + "DeleteData on both providers, and nothing is recorded")]
    [MemberData(nameof(LoneSurrogates))]
    public void A_lone_surrogate_is_refused_by_name(object value)
    {
        foreach (var provider in Providers)
        {
            var insert = Busy(provider);
            var inserted = Assert.Throws<ArgumentException>(() => insert.InsertData("t", ["c"], [value]));
            var delete = Busy(provider);
            var deleted = Assert.Throws<ArgumentException>(() => delete.DeleteData("t", "c", value));

            Assert.Contains("lone surrogate", inserted.Message, StringComparison.Ordinal);
            Assert.Contains("lone surrogate", deleted.Message, StringComparison.Ordinal);
            Assert.Equal("value", inserted.ParamName);
            Assert.Single(insert.Nodes);
            Assert.Single(delete.Nodes);
        }
    }

    [Fact(DisplayName = "[regression pin] a well-formed surrogate pair (an emoji) and text around it are still "
        + "written verbatim")]
    public void A_surrogate_pair_is_still_written()
    {
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"t\" (\"c\") VALUES ('a\U0001F600b'), ('\U0001F600')",
                Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], ["a\U0001F600b"], ["\U0001F600"]).Render(provider)));
        }
    }

    [Fact(DisplayName = "[SEC] the DDL literal and a view parameter refuse U+0000 the same way")]
    public void The_other_writers_refuse_it_too()
    {
        var literal = Assert.Throws<ArgumentException>(() => PostgresLiteral.Of("a\0b"));
        var inlined = Assert.Throws<ArgumentException>(() => ParamLiterals.Of("a\0b"));

        Assert.Contains("U+0000", literal.Message, StringComparison.Ordinal);
        Assert.Contains("U+0000", inlined.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[SEC] a NUL in a later row, a later column or a later delete position records nothing, "
        + "and the message never carries the text of another value")]
    public void A_nul_leaves_no_partial_operation_and_no_echo()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            var inserted = Assert.Throws<ArgumentException>(
                () => builder.InsertData("t", ["a", "b"], [Secret, 1], [Secret, "x\0y"]));
            var deleted = Assert.Throws<ArgumentException>(() => builder.DeleteData("t", "a", Secret, "x\0y"));

            Assert.DoesNotContain(Secret, inserted.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, deleted.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("x\0y", inserted.Message, StringComparison.Ordinal);
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "[SEC] the entity overload refuses a NUL like the column overload")]
    public void The_entity_overload_refuses_it()
    {
        var table = EntityDescriptor.Of("Probe", "probes", true)
            .Property("Label", "label", ColumnType.Text, true, false);
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            Assert.Throws<ArgumentException>(() => builder.InsertData<object>(table, v => [v], "ok", "a\0b"));
            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "[regression pin] a string without U+0000 is still written, and so is an empty one")]
    public void A_clean_string_is_still_written()
    {
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"t\" (\"c\") VALUES ('abc'), ('')",
                Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], ["abc"], [""]).Render(provider)));
        }
    }
}
