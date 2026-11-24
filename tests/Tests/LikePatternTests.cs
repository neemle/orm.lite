using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class LikePatternTests
{
    [Fact(DisplayName = "the escape is the tilde, the clause names it, and the constants agree with one another")]
    public void The_escape_constants_agree()
    {
        Assert.Equal("~", LikePattern.Escape);
        Assert.Equal('~', LikePattern.EscapeChar);
        Assert.Equal(" ESCAPE '~'", LikePattern.EscapeClause);
    }

    [Theory(DisplayName = "EscapeLiteral doubles the escape and escapes the percent sign and the underscore, and nothing else")]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a%b_c~d", "a~%b~_c~~d")]
    public void EscapeLiteral_escapes_the_three_special_characters(string value, string expected)
    {
        Assert.Equal(expected, LikePattern.EscapeLiteral(value));
    }

    [Fact(DisplayName = "Contains, StartsWith and EndsWith wrap the escaped value in the percent signs of the pattern")]
    public void The_pattern_builders_wrap_the_escaped_value()
    {
        Assert.Equal("%a~%b%", LikePattern.Contains("a%b"));
        Assert.Equal("a~_b%", LikePattern.StartsWith("a_b"));
        Assert.Equal("%a~~b", LikePattern.EndsWith("a~b"));
    }
}
