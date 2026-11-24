using Xunit;

namespace Neemle.Orm.Lite.Tests.ScalarConversion;

public sealed class ScalarValueRefusalTests
{
    private sealed class Counting : IConvertible
    {
        internal int Calls { get; private set; }

        private T Tick<T>(T value) { Calls++; return value; }

        public TypeCode GetTypeCode() => Tick(TypeCode.Int32);
        public bool ToBoolean(IFormatProvider? provider) => Tick(true);
        public byte ToByte(IFormatProvider? provider) => Tick((byte)1);
        public char ToChar(IFormatProvider? provider) => Tick('a');
        public DateTime ToDateTime(IFormatProvider? provider) => Tick(DateTime.UnixEpoch);
        public decimal ToDecimal(IFormatProvider? provider) => Tick(1m);
        public double ToDouble(IFormatProvider? provider) => Tick(1.0);
        public short ToInt16(IFormatProvider? provider) => Tick((short)1);
        public int ToInt32(IFormatProvider? provider) => Tick(1);
        public long ToInt64(IFormatProvider? provider) => Tick(1L);
        public sbyte ToSByte(IFormatProvider? provider) => Tick((sbyte)1);
        public float ToSingle(IFormatProvider? provider) => Tick(1f);
        public string ToString(IFormatProvider? provider) => Tick("counted");
        public object ToType(Type conversionType, IFormatProvider? provider) => Tick<object>(1);
        public ushort ToUInt16(IFormatProvider? provider) => Tick((ushort)1);
        public uint ToUInt32(IFormatProvider? provider) => Tick(1u);
        public ulong ToUInt64(IFormatProvider? provider) => Tick(1UL);
    }

    [Fact(DisplayName = "AC4 a Double source is refused for int, long and short by name, never truncated")]
    public void A_double_is_never_truncated_into_an_integer()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<int>(1.5), "Int32", "Double");
        ScalarRaw.Speaks(ScalarRaw.Refused<long>(1.5), "Int64", "Double");
        ScalarRaw.Speaks(ScalarRaw.Refused<short>(2.0), "Int16", "Double");
        ScalarRaw.Speaks(ScalarRaw.Refused<int>(1.5f), "Int32", "Single");
    }

    [Fact(DisplayName = "AC4 a Decimal source is refused for int, long, double and float, as SQLite decimal text is")]
    public void A_decimal_source_is_refused_for_integral_and_floating_targets()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<int>(7m), "Int32", "Decimal");
        ScalarRaw.Speaks(ScalarRaw.Refused<long>(7m), "Int64", "Decimal");
        ScalarRaw.Speaks(ScalarRaw.Refused<double>(7m), "Double", "Decimal");
        ScalarRaw.Speaks(ScalarRaw.Refused<float>(7m), "Single", "Decimal");
        ScalarRaw.Speaks(ScalarRaw.Refused<int?>(7m), "Int32?", "Decimal");
    }

    [Fact(DisplayName = "AC4 a decimal text is refused for int and double, as the Decimal source is")]
    public void Decimal_text_is_refused_for_integral_and_floating_targets()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<int>("12.3400"), "Int32", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<double>("12.3400"), "Double", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<long>("7"), "Int64", "String");
    }

    [Fact(DisplayName = "AC4 a Double or Single source is refused for decimal and the message says why")]
    public void A_floating_source_is_refused_for_decimal_with_the_reason()
    {
        var fromDouble = ScalarRaw.Refused<decimal>(1.5);
        var fromSingle = ScalarRaw.Refused<decimal?>(1.5f);

        ScalarRaw.Speaks(fromDouble, "Decimal", "Double");
        ScalarRaw.Speaks(fromSingle, "Decimal?", "Single");
        Assert.Contains("rounded through a floating point number", fromDouble.Message);
        Assert.Contains("read a decimal column as a decimal", fromSingle.Message);
    }

    [Fact(DisplayName = "AC4 an integer other than 0 and 1 is refused for bool and the message says why")]
    public void Only_zero_and_one_are_a_bool()
    {
        var two = ScalarRaw.Refused<bool>(2L);

        ScalarRaw.Speaks(two, "Boolean", "Int64");
        Assert.Contains("only the integers 0 and 1 are a bool", two.Message);
        Assert.NotNull(ScalarRaw.Refused<bool>(-1L));
        Assert.NotNull(ScalarRaw.Refused<bool?>(2));
        Assert.NotNull(ScalarRaw.Refused<bool>(ulong.MaxValue));
    }

    // ORML-73 (1): PostgreSQL hands a bool column back as a Boolean. It is refused for every numeric target by name
    // (Speck R2/D3), and mutant M22 (accept a Boolean source as 0 or 1) used to survive all of these tests.
    [Fact(DisplayName = "AC4 a Boolean source is refused for int, long, decimal and double by name, "
        + "with no stored value in the message")]
    public void A_boolean_source_is_refused_for_numeric_targets()
    {
        var refused = new[]
        {
            ("Int32", ScalarRaw.Refused<int>(true)),
            ("Int64", ScalarRaw.Refused<long>(true)),
            ("Decimal", ScalarRaw.Refused<decimal>(true)),
            ("Double", ScalarRaw.Refused<double>(true)),
        };

        foreach (var (asked, error) in refused)
        {
            ScalarRaw.Speaks(error, asked, "Boolean");
            ScalarRaw.KeepsQuiet(error, "True");
            Assert.Contains("no conversion between these types is defined", error.Message);
        }
        ScalarRaw.Speaks(ScalarRaw.Refused<int>(false), "Int32", "Boolean");
    }

    // ORML-73 (2): a finite double beyond the float range used to come back as Infinity.
    [Fact(DisplayName = "AC4 a finite double beyond the float range is an OverflowException for float and float?, "
        + "never an Infinity, and the stored number is not in the message")]
    public void A_double_beyond_the_float_range_overflows()
    {
        var plain = ScalarRaw.Overflowed<float>(1e300);
        var lifted = ScalarRaw.Overflowed<float?>(-1e300);

        Assert.StartsWith("ScalarAsync<Single>", plain.Message);
        Assert.StartsWith("ScalarAsync<Single?>", lifted.Message);
        Assert.Contains("Double", plain.Message);
        ScalarRaw.KeepsQuiet(plain, "1E+300");
    }

    [Fact(DisplayName = "AC4 a double that fits a float is narrowed, and a stored NaN or infinity is read as it was")]
    public void A_stored_non_finite_double_still_reads_as_a_float()
    {
        Assert.Equal(1.5f, ScalarRaw.Of<float>(1.5));
        Assert.Equal(0f, ScalarRaw.Of<float>(1e-300));
        Assert.True(float.IsNaN(ScalarRaw.Of<float>(double.NaN)));
        Assert.True(float.IsPositiveInfinity(ScalarRaw.Of<float>(double.PositiveInfinity)));
        Assert.True(float.IsNegativeInfinity(ScalarRaw.Of<float>(double.NegativeInfinity)));
    }

    [Fact(DisplayName = "AC4 text is refused for bool and a Double source is refused for bool")]
    public void Text_and_floating_sources_are_refused_for_bool()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<bool>("1"), "Boolean", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<bool>("true"), "Boolean", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<bool>(1.0), "Boolean", "Double");
    }

    [Fact(DisplayName = "AC4 text that is not a number is refused for int and long")]
    public void Text_is_refused_for_an_integer_target()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<int>("abc"), "Int32", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<int>("7"), "Int32", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<int?>("7"), "Int32?", "String");
    }

    [Fact(DisplayName = "AC4 string is refused for every non-text source and never stringified")]
    public void A_string_target_refuses_a_non_text_source()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<string>(7L), "String", "Int64");
        ScalarRaw.Speaks(ScalarRaw.Refused<string>(7), "String", "Int32");
        ScalarRaw.Speaks(ScalarRaw.Refused<string>(1.5), "String", "Double");
        ScalarRaw.Speaks(ScalarRaw.Refused<string>(true), "String", "Boolean");
        ScalarRaw.Speaks(ScalarRaw.Refused<string>(Guid.Empty), "String", "Guid");
    }

    [Fact(DisplayName = "AC4 text that is not a Guid is refused and the message names the form the library writes")]
    public void Malformed_guid_text_is_refused_with_the_reason()
    {
        var error = ScalarRaw.Refused<Guid>("not-a-guid");

        ScalarRaw.Speaks(error, "Guid", "String");
        Assert.Contains("the text is not a Guid in the form the library writes", error.Message);
        Assert.NotNull(ScalarRaw.Refused<Guid?>("not-a-guid"));
    }

    [Fact(DisplayName = "AC4 D1 malformed decimal text is an InvalidCastException, no longer a FormatException")]
    public void Malformed_decimal_text_is_an_invalid_cast()
    {
        foreach (var text in new[] { "abc", "1e3", " 1", "1,000", "", "1.2.3", "--1" })
            ScalarRaw.Speaks(ScalarRaw.Refused<decimal>(text), "Decimal", "String");
        var reason = ScalarRaw.Refused<decimal>("abc").Message;

        Assert.Contains("the text is not a Decimal in the form the library writes", reason);
    }

    [Fact(DisplayName = "AC4 malformed DateTime and DateOnly text is refused with the form reason")]
    public void Malformed_moment_text_is_refused_with_the_reason()
    {
        var moment = ScalarRaw.Refused<DateTime>("garbage");
        var day = ScalarRaw.Refused<DateOnly>("garbage");

        ScalarRaw.Speaks(moment, "DateTime", "String");
        ScalarRaw.Speaks(day, "DateOnly", "String");
        Assert.Contains("the text is not a DateTime in the form the library writes", moment.Message);
        Assert.Contains("the text is not a DateOnly in the form the library writes", day.Message);
    }

    [Fact(DisplayName = "AC4 types outside the model columns are refused with the generic reason")]
    public void Types_that_are_not_model_columns_are_refused()
    {
        var offset = ScalarRaw.Refused<DateTimeOffset>(DateTime.UnixEpoch);

        ScalarRaw.Speaks(offset, "DateTimeOffset", "DateTime");
        Assert.Contains("no conversion between these types is defined", offset.Message);
        ScalarRaw.Speaks(ScalarRaw.Refused<DateTimeOffset>("2026-03-04T05:06:07Z"), "DateTimeOffset", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<byte[]>("abc"), "Byte[]", "String");
        ScalarRaw.Speaks(ScalarRaw.Refused<TimeSpan>(5L), "TimeSpan", "Int64");
        ScalarRaw.Speaks(ScalarRaw.Refused<char>("a"), "Char", "String");
    }

    [Fact(DisplayName = "AC4 a Guid, a DateTime and a DateOnly source are refused for each other's targets")]
    public void Native_sources_are_refused_for_a_different_target()
    {
        ScalarRaw.Speaks(ScalarRaw.Refused<Guid>(1L), "Guid", "Int64");
        ScalarRaw.Speaks(ScalarRaw.Refused<DateTime>(1L), "DateTime", "Int64");
        ScalarRaw.Speaks(ScalarRaw.Refused<DateOnly>(DateTime.UnixEpoch), "DateOnly", "DateTime");
        ScalarRaw.Speaks(ScalarRaw.Refused<bool>(Guid.Empty), "Boolean", "Guid");
    }

    [Fact(DisplayName = "AC4 [SEC] no refusal message carries the stored text and none chains an inner exception")]
    public void A_refusal_keeps_the_stored_text_out()
    {
        const string stored = "x7Q-9981";

        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<int>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<Guid>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<decimal>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<DateTime>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<DateOnly>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<bool>(stored), stored);
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<double>(stored), stored);
    }

    [Fact(DisplayName = "AC4 [SEC] a refusal of a number never carries the stored number")]
    public void A_refusal_keeps_the_stored_number_out()
    {
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<int>(987654.321), "987654");
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<int>(987654321.123m), "987654321");
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<decimal>(987654.321), "987654");
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<bool>(987654321012L), "987654321012");
        ScalarRaw.KeepsQuiet(ScalarRaw.Refused<string>(987654321012L), "987654321012");
    }

    [Fact(DisplayName = "AC4 [SEC] a raw object implementing IConvertible is never asked to convert itself")]
    public void A_custom_convertible_is_never_run()
    {
        var probe = new Counting();

        Assert.NotNull(ScalarRaw.Refused<int>(probe));
        Assert.NotNull(ScalarRaw.Refused<long>(probe));
        Assert.NotNull(ScalarRaw.Refused<double>(probe));
        Assert.NotNull(ScalarRaw.Refused<decimal>(probe));
        Assert.NotNull(ScalarRaw.Refused<bool>(probe));
        Assert.NotNull(ScalarRaw.Refused<string>(probe));
        Assert.NotNull(ScalarRaw.Refused<DateTime>(probe));
        Assert.Equal(0, probe.Calls);
    }

    [Fact(DisplayName = "AC4 [SEC] an object or IConvertible read of a custom raw returns the same instance")]
    public void A_custom_convertible_read_as_object_is_returned_as_it_is()
    {
        var probe = new Counting();

        Assert.Same(probe, ScalarRaw.Of<object>(probe));
        Assert.Same(probe, ScalarRaw.Of<IConvertible>(probe));
        Assert.Equal(0, probe.Calls);
    }
}
