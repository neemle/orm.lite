using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class BackslashLiteralTests
{
    public static IEnumerable<object[]> Texts() =>
    [
        [@"a\b", @"E'a\\b'", @"'a\b'"],
        [@"\", @"E'\\'", @"'\'"],
        [@"end\", @"E'end\\'", @"'end\'"],
        [@"\'", @"E'\\'''", @"'\'''"],
        [@"'\", @"E'''\\'", @"'''\'"],
        [@"x\'); DROP TABLE t; --", @"E'x\\''); DROP TABLE t; --'", @"'x\''); DROP TABLE t; --'"],
        [@"\\", @"E'\\\\'", @"'\\'"],
        [@"{""k"":""a\""b\n""}", @"E'{""k"":""a\\""b\\n""}'", @"'{""k"":""a\""b\n""}'"],
    ];

    public static IEnumerable<object[]> Plain() =>
    [
        ["it's", "'it''s'"],
        ["", "''"],
        ["~%_", "'~%_'"],
        ["naïve — 日本語", "'naïve — 日本語'"],
    ];

    private static string Insert(ISqlProvider provider, object? value) =>
        Assert.Single(new MigrationBuilder(provider).InsertData("t", ["c"], [value]).Render(provider));

    private static string Delete(ISqlProvider provider, object value) =>
        Assert.Single(new MigrationBuilder(provider).DeleteData("t", "c", value).Render(provider));

    private static void Renders(object value, string postgres, string sqlite)
    {
        Assert.Equal($"INSERT INTO \"t\" (\"c\") VALUES ({postgres})", Insert(PostgresProvider.Instance, value));
        Assert.Equal($"DELETE FROM \"t\" WHERE \"c\" IN ({postgres})", Delete(PostgresProvider.Instance, value));
        Assert.Equal($"INSERT INTO \"t\" (\"c\") VALUES ({sqlite})", Insert(SqliteProvider.Instance, value));
        Assert.Equal($"DELETE FROM \"t\" WHERE \"c\" IN ({sqlite})", Delete(SqliteProvider.Instance, value));
    }

    [Theory(DisplayName = "[SEC] a string holding a backslash is an escape string with the backslash and the quote "
        + "doubled on PostgreSQL, so its meaning does not depend on standard_conforming_strings, and stays a plain "
        + "quote-doubled string on SQLite")]
    [MemberData(nameof(Texts))]
    public void A_backslash_string_is_an_escape_string_on_PostgreSQL(string value, string postgres, string sqlite) =>
        Renders(value, postgres, sqlite);

    [Fact(DisplayName = "[SEC] a backslash char and a document that holds a backslash follow the same rule as a "
        + "string")]
    public void A_char_and_a_document_follow_the_string_rule()
    {
        Renders('\\', @"E'\\'", @"'\'");
        Renders(Jsonb.Parse(@"{""k"":""a\\b""}"), @"E'{""k"":""a\\\\b""}'", @"'{""k"":""a\\b""}'");
    }

    [Theory(DisplayName = "[regression pin] a string without a backslash renders exactly as before on PostgreSQL: "
        + "no E prefix, quotes doubled")]
    [MemberData(nameof(Plain))]
    public void A_string_without_a_backslash_is_unchanged(string value, string literal) =>
        Renders(value, literal, literal);

    [Fact(DisplayName = "[SEC] the entity overload, the DDL literal and a view parameter reach the same "
        + "escape string on PostgreSQL and the plain string on SQLite")]
    public void Every_writer_of_a_literal_reaches_the_same_text()
    {
        var table = EntityDescriptor.Of("Probe", "probes", true)
            .Property("Label", "label", ColumnType.Text, true, false);

        Assert.Equal(
            @"INSERT INTO ""probes"" (""label"") VALUES (E'a\\b')",
            Assert.Single(new MigrationBuilder(PostgresProvider.Instance)
                .InsertData<object>(table, v => [v], @"a\b").Render(PostgresProvider.Instance)));
        Assert.Equal(
            @"INSERT INTO ""probes"" (""label"") VALUES ('a\b')",
            Assert.Single(new MigrationBuilder(SqliteProvider.Instance)
                .InsertData<object>(table, v => [v], @"a\b").Render(SqliteProvider.Instance)));
        Assert.Equal(@"E'a\\b'", PostgresLiteral.Of(@"a\b"));
        Assert.Equal(@"E'a\\b'", PostgresProvider.Instance.CreateSerializer().Fragment(ParamLiterals.Of(@"a\b")));
        Assert.Equal(@"'a\b'", SqliteProvider.Instance.CreateSerializer().Fragment(ParamLiterals.Of(@"a\b")));
    }

    [Fact(DisplayName = "[SEC] a string with a backslash inside a list of values keeps its neighbours plain, so only "
        + "the value that needs it is an escape string")]
    public void Only_the_value_that_needs_it_is_an_escape_string()
    {
        var operation = Assert.Single(new MigrationBuilder(PostgresProvider.Instance)
            .InsertData("t", ["a", "b", "c"], ["x", @"y\z", "w"]).Render(PostgresProvider.Instance));

        Assert.Equal(@"INSERT INTO ""t"" (""a"", ""b"", ""c"") VALUES ('x', E'y\\z', 'w')", operation);
    }
}
