using System.Globalization;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ScalarConversion;

public sealed class ScalarValueTextTests
{
    private static readonly DateTime Written = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc).AddTicks(1234567);

    private static string Shown(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    [Fact(DisplayName = "AC6 decimal reads the text SQLite stores and keeps the scale of 12.3400")]
    public void Decimal_reads_stored_text_and_keeps_its_scale()
    {
        Assert.Equal("12.3400", Shown(ScalarRaw.Of<decimal>("12.3400")));
        Assert.Equal("-0.50", Shown(ScalarRaw.Of<decimal>("-0.50")));
        Assert.Equal("3", Shown(ScalarRaw.Of<decimal>("+3")));
        Assert.Equal("7", Shown(ScalarRaw.Of<decimal>("7")));
    }

    [Fact(DisplayName = "AC6 decimal? reads the stored text and the PostgreSQL Decimal alike and keeps the scale")]
    public void Nullable_decimal_reads_text_and_the_postgres_decimal()
    {
        Assert.Equal("12.3400", Shown(ScalarRaw.Of<decimal?>("12.3400")!.Value));
        Assert.Equal("12.3400", Shown(ScalarRaw.Of<decimal?>(12.3400m)!.Value));
        Assert.Equal("12.3400", Shown(ScalarRaw.Of<decimal>(12.3400m)));
    }

    [Fact(DisplayName = "AC6 Guid reads the text SQLite stores and the Guid PostgreSQL returns")]
    public void Guid_reads_text_and_the_native_guid()
    {
        var expected = new Guid("a0000000-0000-0000-0000-00000000000a");

        Assert.Equal(expected, ScalarRaw.Of<Guid>("a0000000-0000-0000-0000-00000000000a"));
        Assert.Equal(expected, ScalarRaw.Of<Guid>(expected));
        Assert.Equal(expected, ScalarRaw.Of<Guid?>("a0000000-0000-0000-0000-00000000000a"));
    }

    [Fact(DisplayName = "AC6 DateTime reads the library's Z text with the same instant and Kind Utc")]
    public void Datetime_reads_z_text_as_utc()
    {
        var read = ScalarRaw.Of<DateTime>("2026-03-04T05:06:07.1234567Z");

        Assert.Equal(Written, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(Written.Ticks, read.Ticks);
    }

    [Fact(DisplayName = "AC6 DateTime? reads the Z text with Kind Utc")]
    public void Nullable_datetime_reads_z_text_as_utc()
    {
        var read = ScalarRaw.Of<DateTime?>("2026-03-04T05:06:07.1234567Z");

        Assert.Equal(Written, read);
        Assert.Equal(DateTimeKind.Utc, read!.Value.Kind);
    }

    [Fact(DisplayName = "AC6 DateTime reads text that carries no zone as Kind Unspecified, not Local")]
    public void Datetime_reads_zoneless_text_as_unspecified()
    {
        var read = ScalarRaw.Of<DateTime>("2026-03-04T05:06:07");

        Assert.Equal(new DateTime(2026, 3, 4, 5, 6, 7), read);
        Assert.Equal(DateTimeKind.Unspecified, read.Kind);
    }

    // ORML-74: text a foreign writer stored with a numeric offset is read as the same instant in Kind Utc, not as a
    // Local value in the machine's zone; the library itself only ever writes Z.
    [Theory(DisplayName = "AC6 DateTime reads text with a numeric offset as the same instant in Kind Utc, "
        + "and DateTime? does too")]
    [InlineData("2026-03-04T07:06:07.1234567+02:00")]
    [InlineData("2026-03-03T21:06:07.1234567-08:00")]
    [InlineData("2026-03-04T05:06:07.1234567+00:00")]
    [InlineData("2026-03-04T10:36:07.1234567+05:30")]
    public void Datetime_reads_offset_text_as_the_same_instant_in_utc(string text)
    {
        var read = ScalarRaw.Of<DateTime>(text);
        var lifted = ScalarRaw.Of<DateTime?>(text);

        Assert.Equal(Written, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(Written, lifted);
        Assert.Equal(DateTimeKind.Utc, lifted!.Value.Kind);
    }

    [Fact(DisplayName = "AC6 DateTime keeps the Utc and the Unspecified Kind of the DateTime PostgreSQL returns")]
    public void Datetime_source_keeps_its_kind()
    {
        var utc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var unspecified = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);

        Assert.Equal(DateTimeKind.Utc, ScalarRaw.Of<DateTime>(utc).Kind);
        Assert.Equal(DateTimeKind.Unspecified, ScalarRaw.Of<DateTime>(unspecified).Kind);
        Assert.Equal(DateTimeKind.Utc, ScalarRaw.Of<DateTime?>(utc)!.Value.Kind);
        Assert.Equal(DateTimeKind.Unspecified, ScalarRaw.Of<DateTime?>(unspecified)!.Value.Kind);
    }

    [Fact(DisplayName = "AC6 DateOnly reads the text SQLite stores and the DateOnly PostgreSQL returns")]
    public void Dateonly_reads_text_and_the_native_value()
    {
        var expected = new DateOnly(2026, 3, 4);

        Assert.Equal(expected, ScalarRaw.Of<DateOnly>("2026-03-04"));
        Assert.Equal(expected, ScalarRaw.Of<DateOnly>(expected));
        Assert.Equal(expected, ScalarRaw.Of<DateOnly?>("2026-03-04"));
    }

    [Fact(DisplayName = "AC6 Jsonb reads the stored text as it is, spaces and key order untouched")]
    public void Jsonb_reads_the_stored_text_untouched()
    {
        Assert.Equal("{\"b\": 1, \"a\": [2]}", ScalarRaw.Of<Jsonb>("{\"b\": 1, \"a\": [2]}").Json);
        Assert.Equal("{\"a\": 1}", ScalarRaw.Of<Jsonb?>("{\"a\": 1}")!.Json);
    }

    [Fact(DisplayName = "AC6 string reads text from either provider and a json column read as string is unchanged")]
    public void String_reads_text_unchanged()
    {
        Assert.Equal("hello", ScalarRaw.Of<string>("hello"));
        Assert.Equal("{\"a\": 1}", ScalarRaw.Of<string>("{\"a\": 1}"));
        Assert.Equal("", ScalarRaw.Of<string>(""));
    }

    [Fact(DisplayName = "AC6 bool reads the Int64 1 as true and the Int64 0 as false")]
    public void Bool_reads_one_and_zero()
    {
        Assert.True(ScalarRaw.Of<bool>(1L));
        Assert.False(ScalarRaw.Of<bool>(0L));
        Assert.True(ScalarRaw.Of<bool?>(1L));
        Assert.False(ScalarRaw.Of<bool?>(0L));
    }

    [Fact(DisplayName = "AC6 bool reads the other integer widths at 0 or 1 and the PostgreSQL Boolean")]
    public void Bool_reads_the_other_integer_widths_and_the_native_boolean()
    {
        Assert.True(ScalarRaw.Of<bool>(1));
        Assert.True(ScalarRaw.Of<bool>(1u));
        Assert.False(ScalarRaw.Of<bool>((short)0));
        Assert.True(ScalarRaw.Of<bool>(true));
        Assert.False(ScalarRaw.Of<bool>(false));
    }
}
