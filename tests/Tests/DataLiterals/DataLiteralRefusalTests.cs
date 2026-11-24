using System.Numerics;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class DataLiteralRefusalTests
{
    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    public static IEnumerable<object[]> NonFinite() =>
    [
        [double.NaN, "NaN"],
        [double.PositiveInfinity, "Infinity"],
        [double.NegativeInfinity, "-Infinity"],
        [float.NaN, "NaN"],
        [float.PositiveInfinity, "Infinity"],
        [float.NegativeInfinity, "-Infinity"],
    ];

    public static IEnumerable<object[]> Unsupported() =>
    [
        [TimeSpan.FromSeconds(1)],
        [new TimeOnly(1, 2)],
        [new byte[] { 1, 2 }],
        [DBNull.Value],
        [new object()],
        [new BigInteger(5)],
        [Int128.One],
        [Half.One],
        [(nint)5],
    ];

    private static MigrationBuilder Busy(ISqlProvider provider) => new MigrationBuilder(provider).DropTable("earlier");

    [Theory(DisplayName = "NaN and both infinities, as a double or a float, are refused naming the value, and "
        + "nothing is recorded")]
    [MemberData(nameof(NonFinite))]
    public void A_non_finite_number_is_refused_by_name(object value, string token)
    {
        foreach (var provider in Providers)
        {
            var insert = Busy(provider);
            var inserted = Assert.Throws<ArgumentException>(() => insert.InsertData("t", ["c"], [value]));
            var delete = Busy(provider);
            var deleted = Assert.Throws<ArgumentException>(() => delete.DeleteData("t", "c", value));

            Assert.Contains(token, inserted.Message, StringComparison.Ordinal);
            Assert.Contains(token, deleted.Message, StringComparison.Ordinal);
            Assert.Single(insert.Nodes);
            Assert.Single(delete.Nodes);
        }
    }

    [Theory(DisplayName = "a value of a type with no literal form is refused naming the type, and nothing is recorded")]
    [MemberData(nameof(Unsupported))]
    public void An_unsupported_type_is_refused_by_its_type_name(object value)
    {
        var name = value.GetType().FullName!;
        foreach (var provider in Providers)
        {
            var insert = Busy(provider);
            var inserted = Assert.Throws<NotSupportedException>(() => insert.InsertData("t", ["c"], [value]));
            var delete = Busy(provider);
            var deleted = Assert.Throws<NotSupportedException>(() => delete.DeleteData("t", "c", value));

            Assert.Contains(name, inserted.Message, StringComparison.Ordinal);
            Assert.Contains(name, deleted.Message, StringComparison.Ordinal);
            Assert.Single(insert.Nodes);
            Assert.Single(delete.Nodes);
        }
    }

    [Fact(DisplayName = "a refused value in the first position, in a later row or in a later delete position "
        + "records nothing, not even the rows before it")]
    public void A_refusal_records_no_partial_operation()
    {
        foreach (var provider in Providers)
        {
            var builder = Busy(provider);

            Assert.Throws<NotSupportedException>(
                () => builder.InsertData("t", ["a", "b"], [TimeSpan.Zero, 1], ["x", 2]));
            Assert.Throws<NotSupportedException>(
                () => builder.InsertData("t", ["a", "b"], ["ok", 1], ["fine", TimeSpan.Zero]));
            Assert.Throws<ArgumentException>(() => builder.InsertData("t", ["a"], ["ok"], ["fine"], [double.NaN]));
            Assert.Throws<NotSupportedException>(() => builder.DeleteData("t", "a", "x", 2, TimeSpan.Zero));
            Assert.Throws<ArgumentException>(() => builder.DeleteData("t", "a", float.PositiveInfinity, "x"));

            Assert.Single(builder.Nodes);
        }
    }

    [Fact(DisplayName = "a char, a TimeSpan and a NaN carried by an entity row are quoted or refused like the "
        + "same values written as columns")]
    public void The_entity_overload_follows_the_same_rules()
    {
        var table = EntityDescriptor.Of("Probe", "probes", true)
            .Property("Label", "label", ColumnType.Text, true, false);
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"probes\" (\"label\") VALUES ('x'), ('''')",
                Assert.Single(new MigrationBuilder(provider).InsertData<object>(table, v => [v], 'x', '\'').Render(provider)));
            var builder = Busy(provider);

            Assert.Throws<NotSupportedException>(
                () => builder.InsertData<object>(table, v => [v], "ok", TimeSpan.Zero));
            Assert.Throws<ArgumentException>(() => builder.InsertData<object>(table, v => [v], "ok", double.NaN));
            Assert.Single(builder.Nodes);
        }
    }
}
