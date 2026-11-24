using System.Globalization;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class DecimalLiteralRenderingTests
{
    public static IEnumerable<object[]> Decimals() =>
    [
        [1.10m, "1.10"],
        [0.30m, "0.30"],
        [2.25m, "2.25"],
        [-0.25m, "-0.25"],
        [0m, "0"],
        [100m, "100"],
        [1.0m, "1.0"],
        [123456789012.12345678m, "123456789012.12345678"],
        [12345678901234567.89m, "12345678901234567.89"],
        [decimal.MaxValue, "79228162514264337593543950335"],
        [decimal.MinValue, "-79228162514264337593543950335"],
    ];

    private static string Insert(ISqlProvider provider, object value) =>
        Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], [value]).Render(provider));

    private static string Delete(ISqlProvider provider, object value) =>
        Assert.Single(new MigrationBuilder(provider).DeleteData("t", "c", value).Render(provider));

    private static string View(ISqlProvider provider, object value)
    {
        var compare = new Compare(CompareOp.Greater, new ColumnRef("price"), new Param(0, value));
        var query = new Select([new ColumnRef("price")], new TableRef("t"), new Where([compare]));
        return provider.CreateSerializer().Serialize(new CreateView("v", query)).Text;
    }

    [Theory(DisplayName = "a decimal is written on SQLite as single-quoted invariant text with its scale and every "
        + "digit, in an insert and in a delete, and stays a bare number on PostgreSQL")]
    [MemberData(nameof(Decimals))]
    public void A_decimal_is_quoted_text_on_SQLite_and_bare_on_PostgreSQL(decimal value, string text)
    {
        Assert.Equal($"INSERT INTO \"t\" (\"c\") VALUES ('{text}')", Insert(SqliteProvider.Instance, value));
        Assert.Equal($"DELETE FROM \"t\" WHERE \"c\" IN ('{text}')", Delete(SqliteProvider.Instance, value));
        Assert.Equal($"INSERT INTO \"t\" (\"c\") VALUES ({text})", Insert(PostgresProvider.Instance, value));
        Assert.Equal($"DELETE FROM \"t\" WHERE \"c\" IN ({text})", Delete(PostgresProvider.Instance, value));
    }

    [Fact(DisplayName = "a decimal under a comma-decimal thread culture is written with a point on SQLite")]
    public void The_text_is_invariant_under_a_comma_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            Thread.CurrentThread.CurrentCulture = comma;

            Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES ('1.10')", Insert(SqliteProvider.Instance, 1.10m));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "the entity overload, a view parameter and the node a view definition holds reach the same "
        + "quoted text on SQLite and the same bare text on PostgreSQL")]
    public void Every_writer_of_a_decimal_literal_agrees()
    {
        var table = EntityDescriptor.Of("Probe", "probes", true)
            .Property("Amount", "amount", ColumnType.Decimal, true, false, 12, 4);

        Assert.Equal(
            "INSERT INTO \"probes\" (\"amount\") VALUES ('1.10')",
            Assert.Single(new MigrationBuilder(SqliteProvider.Instance)
                .InsertData<object>(table, v => [v], 1.10m).Render(SqliteProvider.Instance)));
        Assert.Equal(
            "INSERT INTO \"probes\" (\"amount\") VALUES (1.10)",
            Assert.Single(new MigrationBuilder(PostgresProvider.Instance)
                .InsertData<object>(table, v => [v], 1.10m).Render(PostgresProvider.Instance)));
        Assert.Equal("1.10", PostgresLiteral.Of(1.10m));
        Assert.Equal("'1.10'", SqliteProvider.Instance.CreateSerializer().Fragment(ParamLiterals.Of(1.10m)));
        Assert.Equal("1.10", PostgresProvider.Instance.CreateSerializer().Fragment(ParamLiterals.Of(1.10m)));
    }

    [Fact(DisplayName = "a decimal inlined into a view definition is quoted text on SQLite, so it compares with the "
        + "column's decimal collation, and bare on PostgreSQL")]
    public void A_view_definition_inlines_a_decimal_as_the_provider_stores_it()
    {
        var sqlite = View(SqliteProvider.Instance, 1.10m);
        var postgres = View(PostgresProvider.Instance, 1.10m);

        Assert.Contains("> '1.10'", sqlite, StringComparison.Ordinal);
        Assert.DoesNotContain("> 1.10", sqlite, StringComparison.Ordinal);
        Assert.Contains("> 1.10", postgres, StringComparison.Ordinal);
        Assert.DoesNotContain("'1.10'", postgres, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[regression pin] on SQLite a double, a float, an unsigned long, an integer and a flag stay "
        + "bare, and a hand-built bare literal is written as is")]
    public void Other_numbers_and_the_raw_literal_are_unchanged()
    {
        var sqlite = SqliteProvider.Instance;

        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (1.5)", Insert(sqlite, 1.5));
        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (0.1)", Insert(sqlite, 0.1f));
        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (1E-05)", Insert(sqlite, 1E-05));
        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (18446744073709551615)", Insert(sqlite, ulong.MaxValue));
        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (7)", Insert(sqlite, 7));
        Assert.Equal("INSERT INTO \"t\" (\"c\") VALUES (true)", Insert(sqlite, true));
        Assert.Equal("1.10", sqlite.CreateSerializer().Fragment(new BareLit("1.10")));
        Assert.Equal("'1.10'", sqlite.CreateSerializer().Fragment(new BareLit("1.10", ExactDecimal: true)));
        Assert.Equal("1.10", PostgresProvider.Instance.CreateSerializer().Fragment(new BareLit("1.10", true)));
    }
}
