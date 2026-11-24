using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.ScalarConversion;

public sealed class ScalarValueEnumTests
{
    private static readonly (string Input, ScalarMood? Expected)[] Inputs =
    [
        ("Calm", ScalarMood.Calm),
        ("Bright", ScalarMood.Bright),
        ("Dim", ScalarMood.Dim),
        ("calm", ScalarMood.Calm),
        ("BRIGHT", ScalarMood.Bright),
        ("  Dim  ", ScalarMood.Dim),
        ("Zeta", null),
        ("7", null),
        ("1", null),
        ("0", null),
        ("", null),
        ("   ", null),
        ("Calm,Bright", null),
        ("Calm, Bright", null),
        ("Bright2", null),
    ];

    [Fact(DisplayName = "AC3 the enum arm accepts and refuses exactly the inputs OrmEnum.TryParseExact does")]
    public void The_enum_arm_agrees_with_the_row_mapper_rule_on_every_input()
    {
        foreach (var (input, expected) in Inputs)
        {
            Assert.Equal(expected is not null, OrmEnum.TryParseExact<ScalarMood>(input, out var mapped));
            if (expected is { } member)
            {
                Assert.Equal(member, mapped);
                Assert.Equal(member, ScalarRaw.Of<ScalarMood>(input));
            }
            else Assert.Throws<OrmEnumValueException>(() => ScalarRaw.Of<ScalarMood>(input));
        }
    }

    [Fact(DisplayName = "AC6 enum reads the defined member name SQLite and PostgreSQL both store as text")]
    public void Enum_reads_a_member_name()
    {
        Assert.Equal(ScalarMood.Bright, ScalarRaw.Of<ScalarMood>("Bright"));
        Assert.Equal(ScalarMood.Calm, ScalarRaw.Of<ScalarMood>("Calm"));
    }

    [Fact(DisplayName = "AC6 enum? reads a member name and keeps null for NULL")]
    public void Nullable_enum_reads_a_member_name_and_null()
    {
        Assert.Equal(ScalarMood.Dim, ScalarRaw.Of<ScalarMood?>("Dim"));
        Assert.Null(ScalarRaw.Of<ScalarMood?>(null));
        Assert.Null(ScalarRaw.Of<ScalarMood?>(DBNull.Value));
    }

    [Fact(DisplayName = "AC4 [SEC] stored text 7, which is no member, is an OrmEnumValueException and never a number")]
    public void Stored_text_seven_is_refused()
    {
        var error = Assert.Throws<OrmEnumValueException>(() => ScalarRaw.Of<ScalarMood>("7"));

        Assert.Equal("7", error.Value);
    }

    [Fact(DisplayName = "AC4 [SEC] stored text 1, the number of Bright, is an OrmEnumValueException and not Bright")]
    public void Stored_text_one_is_not_the_member_it_numbers()
    {
        var error = Assert.Throws<OrmEnumValueException>(() => ScalarRaw.Of<ScalarMood>("1"));

        Assert.Equal("1", error.Value);
    }

    [Fact(DisplayName = "AC4 an undefined name is an OrmEnumValueException naming table, column and accepted names")]
    public void An_undefined_name_names_where_it_was_read()
    {
        var error = Assert.Throws<OrmEnumValueException>(() => ScalarRaw.Of<ScalarMood>("Zeta"));

        Assert.Equal(ScalarRaw.Table, error.Table);
        Assert.Equal(ScalarRaw.Column, error.Column);
        Assert.Equal("", error.RowKey);
        Assert.Equal(["Calm", "Bright", "Dim"], error.Accepted);
        Assert.Contains(ScalarRaw.Table + "." + ScalarRaw.Column, error.Message);
    }

    [Fact(DisplayName = "AC4 a numeric source is refused for an enum whatever its width")]
    public void A_numeric_source_is_refused_for_an_enum()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<ScalarMood>(1L), "ScalarMood", "Int64");
        ScalarRaw.Speaks(ScalarRaw.Refused<ScalarMood>(0), "ScalarMood", "Int32");
        ScalarRaw.Speaks(ScalarRaw.Refused<ScalarMood?>(1L), "ScalarMood?", "Int64");
        Assert.NotNull(ScalarRaw.Refused<ScalarMood>((short)2));
    }

    [Fact(DisplayName = "AC4 a source that is not text is refused for an enum by name")]
    public void A_non_text_source_is_refused_for_an_enum()
    {
        Assert.NotNull(ScalarRaw.Refused<ScalarMood>(1.0));
        Assert.NotNull(ScalarRaw.Refused<ScalarMood>(true));
        Assert.NotNull(ScalarRaw.Refused<ScalarMood>(Guid.Empty));
        Assert.NotNull(ScalarRaw.Refused<ScalarMood>(ScalarMood.Calm.ToString().ToCharArray()));
    }
}
