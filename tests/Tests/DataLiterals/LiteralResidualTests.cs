using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

// ORML-58: the small residuals of the data-literal work (ORML-8): negative zero, the readable type name of a refused
// generic value, the one timestamp format, and an ArgumentNullException for a null array.
public sealed class LiteralResidualTests
{
    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    public static IEnumerable<object[]> NegativeZeros() => [[-0.0], [-0.0f]];

    [Theory(DisplayName = "negative zero, as a double or a float, renders as 0 in an insert and in a delete")]
    [MemberData(nameof(NegativeZeros))]
    public void Negative_zero_renders_as_zero(object value)
    {
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"t\" (\"c\") VALUES (0)",
                Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], [value]).Render(provider)));
            Assert.Equal(
                "DELETE FROM \"t\" WHERE \"c\" IN (0)",
                Assert.Single(new MigrationBuilder(provider).DeleteData("t", "c", value).Render(provider)));
        }
    }

    [Fact(DisplayName = "a refused generic value is named readably, with no assembly name, version or public key")]
    public void A_generic_type_is_named_without_its_assembly()
    {
        object nested = new Dictionary<string, List<int>>();
        foreach (var provider in Providers)
        {
            var list = Assert.Throws<NotSupportedException>(() => new MigrationBuilder(provider).InsertData("t", ["c"], [new List<int>()]));
            var map = Assert.Throws<NotSupportedException>(() => new MigrationBuilder(provider).DeleteData("t", "c", nested));

            Assert.StartsWith("System.Collections.Generic.List<System.Int32> has no literal form", list.Message);
            Assert.StartsWith(
                "System.Collections.Generic.Dictionary<System.String, System.Collections.Generic.List<System.Int32>> "
                + "has no literal form",
                map.Message);
            Assert.DoesNotContain("Version=", list.Message + map.Message, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "the SQLite codec and the data literals write a moment with the one shared format")]
    public void The_timestamp_format_is_shared()
    {
        var moment = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

        Assert.Equal(ParamLiterals.MomentFormat, SqliteValueCodec.TimestampFormat);
        Assert.Equal(ParamLiterals.Normalise(moment), SqliteValueCodec.Timestamp(moment));
        Assert.Equal("2024-01-02T10:00:00.1234567Z", SqliteValueCodec.Timestamp(moment));
    }

    [Fact(DisplayName = "a null column array, row array or value array is an ArgumentNullException naming it, "
        + "and nothing is recorded")]
    public void A_null_array_is_an_argument_null_exception()
    {
        foreach (var provider in Providers)
        {
            var builder = new MigrationBuilder(provider).DropTable("earlier");

            var columns = Assert.Throws<ArgumentNullException>(() => builder.InsertData("t", null!, [1]));
            var rows = Assert.Throws<ArgumentNullException>(() => builder.InsertData("t", ["c"], null!));
            var values = Assert.Throws<ArgumentNullException>(() => builder.DeleteData("t", "c", null!));

            Assert.Equal("columns", columns.ParamName);
            Assert.Equal("rows", rows.ParamName);
            Assert.Equal("values", values.ParamName);
            Assert.Single(builder.Nodes);
        }
    }
}
