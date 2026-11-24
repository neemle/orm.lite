using System.Globalization;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.DataLiterals;

public sealed class DataLiteralRenderingTests
{
    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    private enum Mood
    {
        Calm,
        Upset,
    }

    private static void Renders(string literal, object? value) => Renders(literal, literal, value);

    private static void Renders(string postgres, string sqlite, object? value)
    {
        string[] literals = [postgres, sqlite];
        for (var i = 0; i < Providers.Length; i++)
        {
            var provider = Providers[i];
            Assert.Equal(
                $"INSERT INTO \"t\" (\"c\") VALUES ({literals[i]})",
                Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], [value]).Render(provider)));
            if (value is null) continue;
            Assert.Equal(
                $"DELETE FROM \"t\" WHERE \"c\" IN ({literals[i]})",
                Assert.Single(new MigrationBuilder(provider).DeleteData("t", "c", value).Render(provider)));
        }
    }

    [Fact(DisplayName = "a Guid renders as a single-quoted lowercase D-format string in an insert and in a "
        + "delete, even when its text starts with digits")]
    public void A_guid_renders_quoted()
    {
        Renders("'a0000000-0000-0000-0000-00000000000a'", Guid.Parse("a0000000-0000-0000-0000-00000000000a"));
        Renders("'12345678-1234-1234-1234-1234567890ab'", Guid.Parse("12345678-1234-1234-1234-1234567890AB"));
    }

    [Fact(DisplayName = "a moment renders as a quoted UTC instant: Utc and Unspecified as given, Local and "
        + "any offset converted, a date as its day")]
    public void A_moment_renders_as_a_quoted_utc_instant()
    {
        var local = new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Local);

        Renders("'2024-01-02T10:00:00.0000000Z'", new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc));
        var unspecified = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Unspecified).AddTicks(1234567);
        Renders("'2024-01-02T10:00:00.1234567Z'", unspecified);
        Renders("'" + local.ToUniversalTime().ToString(Moment, CultureInfo.InvariantCulture) + "'", local);
        Renders("'2024-01-02T04:30:00.0000000Z'", new DateTimeOffset(2024, 1, 2, 10, 0, 0, new TimeSpan(5, 30, 0)));
        Renders("'2024-01-02T18:00:00.0000000Z'", new DateTimeOffset(2024, 1, 2, 10, 0, 0, TimeSpan.FromHours(-8)));
        Renders("'2024-02-29'", new DateOnly(2024, 2, 29));
    }

    [Fact(DisplayName = "an enum renders as its quoted member name, an undefined value as its quoted text, a "
        + "char as a one-character string, a document as its quoted text")]
    public void An_enum_a_char_and_a_document_render_quoted()
    {
        Renders("'Upset'", Mood.Upset);
        Renders("'7'", (Mood)7);
        Renders("'x'", 'x');
        Renders("''''", '\'');
        Renders("'{\"k\":\"it''s\"}'", Jsonb.Parse("{\"k\":\"it's\"}"));
    }

    private static void UnderCommaCulture(Action check)
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            Thread.CurrentThread.CurrentCulture = comma;
            check();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "[regression pin] every integer type renders as its invariant digits under a "
        + "comma-decimal thread culture")]
    public void Integers_render_invariantly_under_a_comma_culture() => UnderCommaCulture(() =>
    {
        Renders("-128", sbyte.MinValue);
        Renders("255", byte.MaxValue);
        Renders("-32768", short.MinValue);
        Renders("65535", ushort.MaxValue);
        Renders("-2147483648", int.MinValue);
        Renders("4294967295", uint.MaxValue);
        Renders("-9223372036854775808", long.MinValue);
        Renders("18446744073709551615", ulong.MaxValue);
    });

    [Fact(DisplayName = "[regression pin] a decimal, a float, a double, a null, a string and a flag render as their "
        + "invariant typed text under a comma-decimal thread culture")]
    public void Fractions_and_scalars_render_invariantly_under_a_comma_culture() => UnderCommaCulture(() =>
    {
        Renders("79228162514264337593543950335", "'79228162514264337593543950335'", decimal.MaxValue);
        Renders("1.5", "'1.5'", 1.5m);
        Renders("0.1", 0.1f);
        Renders("1.5", 1.5f);
        Renders("1E-05", 1E-05);
        Renders("1.5", 1.5);
        Renders("NULL", null);
        Renders("'it''s'", "it's");
        Renders("true", true);
        Renders("false", false);
    });
}
