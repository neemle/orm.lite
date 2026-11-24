using System.Globalization;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ScalarConversion;

public sealed class ScalarValueControlTests
{
    [Fact(DisplayName = "AC5 a null raw value reads as default for every target class")]
    public void Null_reads_as_default()
    {
        Assert.Equal(0, ScalarRaw.Of<int>(null));
        Assert.Equal(0L, ScalarRaw.Of<long>(null));
        Assert.Equal(0m, ScalarRaw.Of<decimal>(null));
        Assert.False(ScalarRaw.Of<bool>(null));
        Assert.Equal(Guid.Empty, ScalarRaw.Of<Guid>(null));
        Assert.Equal(default(DateTime), ScalarRaw.Of<DateTime>(null));
        Assert.Equal(ScalarMood.Calm, ScalarRaw.Of<ScalarMood>(null));
        Assert.Null(ScalarRaw.Of<string>(null));
        Assert.Null(ScalarRaw.Of<object>(null));
    }

    [Fact(DisplayName = "AC5 DBNull reads as default for every target class, as null does")]
    public void Dbnull_reads_as_default()
    {
        Assert.Equal(0, ScalarRaw.Of<int>(DBNull.Value));
        Assert.Equal(0m, ScalarRaw.Of<decimal>(DBNull.Value));
        Assert.False(ScalarRaw.Of<bool>(DBNull.Value));
        Assert.Equal(Guid.Empty, ScalarRaw.Of<Guid>(DBNull.Value));
        Assert.Equal(default(DateTime), ScalarRaw.Of<DateTime>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<string>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<object>(DBNull.Value));
    }

    [Fact(DisplayName = "AC5 null and DBNull read as null for every nullable target")]
    public void Nullable_targets_read_null_for_null_and_dbnull()
    {
        Assert.Null(ScalarRaw.Of<int?>(null));
        Assert.Null(ScalarRaw.Of<int?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<decimal?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<bool?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<Guid?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<DateTime?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<DateOnly?>(DBNull.Value));
        Assert.Null(ScalarRaw.Of<ScalarMood?>(DBNull.Value));
    }

    [Fact(DisplayName = "AC2 object returns the raw provider value, Int64 on SQLite and Int32 on PostgreSQL")]
    public void Object_returns_the_raw_provider_value()
    {
        Assert.IsType<long>(ScalarRaw.Of<object?>(7L));
        Assert.IsType<int>(ScalarRaw.Of<object?>(7));
        Assert.Equal(7L, ScalarRaw.Of<object?>(7L));
        Assert.Equal(7, ScalarRaw.Of<object?>(7));
    }

    [Fact(DisplayName = "AC8 a target the raw value is assignable to returns the same instance")]
    public void An_assignable_target_returns_the_raw_instance()
    {
        var text = new string('x', 3);

        Assert.Same(text, ScalarRaw.Of<string>(text));
        Assert.Same(text, ScalarRaw.Of<object>(text));
        Assert.Same(text, ScalarRaw.Of<IComparable>(text));
        Assert.Same(text, ScalarRaw.Of<IEnumerable<char>>(text));
    }

    [Fact(DisplayName = "AC8 a value type read as ValueType, IComparable or IFormattable keeps its boxed value")]
    public void Interface_and_base_targets_keep_a_boxed_value()
    {
        Assert.Equal((object)7L, ScalarRaw.Of<ValueType>(7L));
        Assert.Equal((object)7L, ScalarRaw.Of<IComparable>(7L));
        Assert.Equal((object)7, ScalarRaw.Of<IFormattable>(7));
    }

    [Fact(DisplayName = "AC8 a raw value of exactly the target type is returned unchanged for every column type")]
    public void An_exact_type_is_returned_unchanged()
    {
        var moment = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        Assert.Equal(7L, ScalarRaw.Of<long>(7L));
        Assert.Equal(1.5, ScalarRaw.Of<double>(1.5));
        Assert.Equal(7m, ScalarRaw.Of<decimal>(7m));
        Assert.True(ScalarRaw.Of<bool>(true));
        Assert.Equal(moment, ScalarRaw.Of<DateTime>(moment));
        Assert.Equal(new DateOnly(2026, 3, 4), ScalarRaw.Of<DateOnly>(new DateOnly(2026, 3, 4)));
        Assert.Equal(ScalarMood.Dim, ScalarRaw.Of<ScalarMood>(ScalarMood.Dim));
    }

    [Fact(DisplayName = "AC1 the decimal text style is a leading sign and a decimal point, nothing wider")]
    public void The_decimal_text_style_is_narrow()
    {
        Assert.Equal(NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, ScalarValue.DecimalStyle);
    }

    [Fact(DisplayName = "AC4 [SEC] decimal and moment text parse invariantly under a comma-decimal thread culture")]
    public void Text_parses_with_the_invariant_culture_whatever_the_thread_culture_is()
    {
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        comma.NumberFormat.NumberGroupSeparator = ".";
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = comma;
        try
        {
            Assert.Equal(12.34m, decimal.Parse("12,34", comma));
            Assert.False(decimal.TryParse("12.34", NumberStyles.AllowDecimalPoint, comma, out _));
            Assert.Equal("12.3400", ScalarRaw.Of<decimal>("12.3400").ToString(CultureInfo.InvariantCulture));
            Assert.Equal(new DateOnly(2026, 3, 4), ScalarRaw.Of<DateOnly>("2026-03-04"));
            Assert.Equal(DateTimeKind.Utc, ScalarRaw.Of<DateTime>("2026-03-04T05:06:07.1234567Z").Kind);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
