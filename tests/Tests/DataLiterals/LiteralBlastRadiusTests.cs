using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class LiteralBlastRadiusTests
{
    private enum Mood
    {
        Calm,
    }

    [Fact(DisplayName = "[regression pin] a view definition keeps the same node for an id, a moment, a day, "
        + "an enum, a document, a string, a number, a flag and a null")]
    public void The_view_path_keeps_its_nodes_for_the_kinds_it_already_handled()
    {
        var id = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
        var moment = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        Assert.Equal(new StringLit("a0000000-0000-0000-0000-00000000000a"), ParamLiterals.Of(id));
        Assert.Equal(new StringLit("2026-03-04T05:06:07.0000000Z"), ParamLiterals.Of(moment));
        Assert.Equal(new StringLit("2026-03-04"), ParamLiterals.Of(new DateOnly(2026, 3, 4)));
        Assert.Equal(new StringLit("Calm"), ParamLiterals.Of(Mood.Calm));
        Assert.Equal(new StringLit("{\"k\":1}"), ParamLiterals.Of(Jsonb.Parse("{\"k\":1}")));
        Assert.Equal(new StringLit("it's"), ParamLiterals.Of("it's"));
        Assert.Equal(new IntLit(7), ParamLiterals.Of(7));
        Assert.Equal(new IntLit(long.MinValue), ParamLiterals.Of(long.MinValue));
        Assert.Equal(new BoolLit(true), ParamLiterals.Of(true));
        Assert.Equal(new BareLit("2.25", ExactDecimal: true), ParamLiterals.Of(2.25m));
        Assert.Equal(new BareLit("1.5"), ParamLiterals.Of(1.5));
        Assert.Equal(new NullLit(), ParamLiterals.Of(null));
    }

    [Fact(DisplayName = "a view definition now quotes a char and refuses a TimeSpan, a TimeOnly and a "
        + "non-finite number, and a collection keeps its own refusal")]
    public void The_view_path_changes_only_for_the_leftover_kinds()
    {
        Assert.Equal(new StringLit("x"), ParamLiterals.Of('x'));
        Assert.Throws<NotSupportedException>(() => ParamLiterals.Of(TimeSpan.FromSeconds(1)));
        Assert.Throws<NotSupportedException>(() => ParamLiterals.Of(new TimeOnly(1, 2)));
        Assert.Throws<ArgumentException>(() => ParamLiterals.Of(double.NaN));
        var collection = Assert.Throws<NotSupportedException>(() => ParamLiterals.Of(new[] { 1, 2 }));
        Assert.StartsWith("a view definition is stored in the database", collection.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the DDL literal quotes a Guid and a moment and refuses a TimeSpan and a "
        + "non-finite number")]
    public void The_ddl_literal_follows_the_same_rules()
    {
        var id = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
        var moment = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal("'a0000000-0000-0000-0000-00000000000a'", PostgresLiteral.Of(id));
        Assert.Equal("'2024-01-02T10:00:00.0000000Z'", PostgresLiteral.Of(moment));
        Assert.Equal("'it''s'", PostgresLiteral.Of("it's"));
        Assert.Equal("1.5", PostgresLiteral.Of(1.5));
        Assert.Throws<NotSupportedException>(() => PostgresLiteral.Of(TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentException>(() => PostgresLiteral.Of(double.NaN));
    }

    [Fact(DisplayName = "a deviation item of the corpus index escapes a pipe and a line break in a text, "
        + "and names a missing old or new text")]
    public void A_deviation_row_escapes_a_pipe_and_a_line_break()
    {
        var changed = new CorpusIndex.Deviation(
            "g/c", CorpusIndex.DeviationKind.Changed, "a | b\r\nc", "d\ne", "x | y");
        var removed = new CorpusIndex.Deviation("g/c", CorpusIndex.DeviationKind.Removed, "a", null, "r");
        var added = new CorpusIndex.Deviation("g/c", CorpusIndex.DeviationKind.Added, null, "n", "r");

        Assert.Equal("- `g/c`, Changed. Old text: `a \\| b\\nc`. New text: `d\\ne`. Reason: x \\| y\n",
            CorpusIndex.Row(changed));
        Assert.Equal("- `g/c`, Removed. Old text: `a`. New text: (removed). Reason: r\n", CorpusIndex.Row(removed));
        Assert.Equal("- `g/c`, Added. Old text: (none). New text: `n`. Reason: r\n", CorpusIndex.Row(added));
    }

    [Fact(DisplayName = "a deviation item of the corpus index is word-wrapped at 120 characters and loses no word")]
    public void A_deviation_item_is_wrapped_at_120_characters()
    {
        var reason = string.Join(' ', Enumerable.Repeat("word", 80));
        var deviation = new CorpusIndex.Deviation("g/c", CorpusIndex.DeviationKind.Changed, "a", "b", reason);

        var lines = CorpusIndex.Row(deviation).TrimEnd('\n').Split('\n');

        Assert.True(lines.Length > 1);
        Assert.All(lines, line => Assert.True(line.Length <= 120, line));
        Assert.All(lines.Skip(1), line => Assert.StartsWith("  ", line));
        Assert.Equal("- `g/c`, Changed. Old text: `a`. New text: `b`. Reason: " + reason,
            string.Join(' ', lines.Select(line => line.Trim())));
    }
}
