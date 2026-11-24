using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.HostileNames;

public sealed class PostgresNamesTests
{
    [Theory(DisplayName = "an identifier is quoted and every embedded double quote is doubled")]
    [InlineData("docs", "\"docs\"")]
    [InlineData("we\"ird", "\"we\"\"ird\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("it's", "\"it's\"")]
    [InlineData("100%", "\"100%\"")]
    public void An_identifier_is_quoted(string name, string expected) =>
        Assert.Equal(expected, PostgresNames.Ident(name).Text);

    [Theory(DisplayName = "a string literal is quoted and every embedded single quote is doubled")]
    [InlineData("docs", "'docs'")]
    [InlineData("it's", "'it''s'")]
    [InlineData("a\"b", "'a\"b'")]
    [InlineData("'; DROP TABLE x; --", "'''; DROP TABLE x; --'")]
    public void A_string_literal_is_quoted(string value, string expected) =>
        Assert.Equal(expected, PostgresNames.Text(value).Text);

    [Theory(DisplayName = "a name placed in a RAISE message or a format() template has every percent sign doubled")]
    [InlineData("plain", "plain")]
    [InlineData("100%", "100%%")]
    [InlineData("a%%b", "a%%%%b")]
    [InlineData("%I", "%%I")]
    public void A_percent_sign_is_doubled(string value, string expected) =>
        Assert.Equal(expected, PostgresNames.Percent(value).Text);

    [Fact(DisplayName = "a format identifier is the quoted identifier with its percent signs doubled")]
    public void A_format_identifier_is_quoted_then_percent_doubled() =>
        Assert.Equal("\"t%%\"\"x\"", PostgresNames.FormatIdent("t%\"x").Text);

    [Fact(DisplayName = "a regclass literal is the quoted identifier as a string literal value")]
    public void A_regclass_literal_holds_the_quoted_identifier() =>
        Assert.Equal(new StringLit("\"we\"\"ird\""), PostgresNames.Regclass("we\"ird"));

    [Fact(DisplayName = "a template is filled from escaped parts only, in order, with a list joined by a comma")]
    public void A_template_is_filled_from_escaped_parts()
    {
        var list = PostgresNames.List(new[] { "a", "it's" }.Select(PostgresNames.Text));

        var table = PostgresNames.Text(PostgresNames.Ident("we\"ird").Text);

        var text = PostgresNames.Splice("to_regclass({0}) {1} {2}", table, PostgresNames.Count(2), list).Text;

        Assert.Equal("to_regclass('\"we\"\"ird\"') 2 'a', 'it''s'", text);
    }
}
