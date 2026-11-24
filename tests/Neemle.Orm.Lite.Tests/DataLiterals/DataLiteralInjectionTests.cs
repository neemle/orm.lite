using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.DataLiterals;

public sealed class DataLiteralInjectionTests
{
    private const string Payload = "1); DROP TABLE x; --";

    private static readonly ISqlProvider[] Providers = [PostgresProvider.Instance, SqliteProvider.Instance];

    private enum Mood
    {
        Calm,
    }

    private sealed class HostileObject
    {
        public override string ToString() => Payload;
    }

    private sealed class HostileFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => Payload;

        public override string ToString() => Payload;
    }

    private static string Insert(ISqlProvider provider, params object?[] row) =>
        Assert.Single(new MigrationBuilder(provider).InsertData("t", ["a", "b"], row).Render(provider));

    [Fact(DisplayName = "[SEC] an object or a formattable whose text is SQL is refused everywhere a literal "
        + "is made, and no message carries the hostile text")]
    public void A_hostile_value_is_refused_and_never_echoed()
    {
        foreach (var hostile in new object[] { new HostileObject(), new HostileFormattable() })
        {
            var errors = new List<NotSupportedException>();
            foreach (var provider in Providers)
            {
                errors.Add(Assert.Throws<NotSupportedException>(() => new MigrationBuilder(provider).InsertData("t", ["c"], [hostile])));
                errors.Add(Assert.Throws<NotSupportedException>(() => new MigrationBuilder(provider).DeleteData("t", "c", hostile)));
            }
            errors.Add(Assert.Throws<NotSupportedException>(() => PostgresLiteral.Of(hostile)));
            errors.Add(Assert.Throws<NotSupportedException>(() => ParamLiterals.Of(hostile)));

            Assert.All(errors, e => Assert.DoesNotContain("DROP", e.Message, StringComparison.Ordinal));
            Assert.All(errors, e => Assert.Contains(hostile.GetType().FullName!, e.Message, StringComparison.Ordinal));
        }
    }

    [Fact(DisplayName = "[SEC] a quote character renders doubled inside its quotes, and a string holding a "
        + "quote, a semicolon, a comment marker and a bracket renders quote-doubled")]
    public void Quotes_cannot_end_the_literal()
    {
        foreach (var provider in Providers)
        {
            Assert.Equal("INSERT INTO \"t\" (\"a\", \"b\") VALUES ('''', 'x')", Insert(provider, '\'', "x"));
            Assert.Equal(
                "INSERT INTO \"t\" (\"a\", \"b\") VALUES ('x''); DROP TABLE t; --', 'y')",
                Insert(provider, "x'); DROP TABLE t; --", "y"));
            Assert.Equal(
                "DELETE FROM \"t\" WHERE \"c\" IN ('''); DROP TABLE t; --')",
                Assert.Single(new MigrationBuilder(provider).DeleteData("t", "c", "'); DROP TABLE t; --").Render(provider)));
        }
    }

    [Fact(DisplayName = "[SEC] a document holding a quote and an enum value with no member name render as "
        + "quoted, quote-doubled text")]
    public void A_document_and_an_undefined_enum_render_quoted()
    {
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"t\" (\"a\", \"b\") VALUES ('{\"k\":\"it''s''); DROP TABLE t; --\"}', '9')",
                Insert(provider, Jsonb.Parse("{\"k\":\"it's'); DROP TABLE t; --\"}"), (Mood)9));
        }
    }

    [Fact(DisplayName = "[SEC] a refusal message names a type or a fixed token and never the text of another "
        + "value of the same statement")]
    public void A_refusal_does_not_echo_a_value()
    {
        foreach (var provider in Providers)
        {
            var builder = new MigrationBuilder(provider);

            var unsupported = Assert.Throws<NotSupportedException>(
                () => builder.InsertData("t", ["a", "b"], ["secret-value", TimeSpan.Zero]));
            var infinite = Assert.Throws<ArgumentException>(
                () => builder.InsertData("t", ["a", "b"], ["secret-value", double.PositiveInfinity]));
            var deleted = Assert.Throws<NotSupportedException>(
                () => builder.DeleteData("t", "c", "secret-value", TimeSpan.Zero));

            Assert.DoesNotContain("secret-value", unsupported.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-value", infinite.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-value", deleted.Message, StringComparison.Ordinal);
            Assert.Empty(builder.Nodes);
        }
    }

    [Fact(DisplayName = "[SEC] a plain non-ASCII string and an empty string are written verbatim inside their quotes")]
    public void Unicode_text_is_written_verbatim()
    {
        foreach (var provider in Providers)
        {
            Assert.Equal(
                "INSERT INTO \"t\" (\"a\", \"b\") VALUES ('naïve — 日本語 \U0001F600', '')",
                Insert(provider, "naïve — 日本語 \U0001F600", ""));
        }
    }
}
