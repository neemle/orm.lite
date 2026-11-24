using Xunit;

namespace Neemle.Orm.Lite.Tests.ScalarConversion;

public sealed class ScalarValueIntegerTests
{
    [Fact(DisplayName = "AC6 sbyte reads the Int64 a SQLite integer column returns")]
    public void Sbyte_reads_the_int64_of_sqlite() => Assert.Equal((sbyte)-5, ScalarRaw.Of<sbyte>(-5L));

    [Fact(DisplayName = "AC6 byte reads the Int64 a SQLite integer column returns")]
    public void Byte_reads_the_int64_of_sqlite() => Assert.Equal((byte)200, ScalarRaw.Of<byte>(200L));

    [Fact(DisplayName = "AC6 short reads the Int64 a SQLite integer column returns")]
    public void Short_reads_the_int64_of_sqlite() => Assert.Equal((short)-300, ScalarRaw.Of<short>(-300L));

    [Fact(DisplayName = "AC6 ushort reads the Int64 a SQLite integer column returns")]
    public void Ushort_reads_the_int64_of_sqlite() => Assert.Equal((ushort)60000, ScalarRaw.Of<ushort>(60000L));

    [Fact(DisplayName = "AC6 int reads the Int64 a SQLite integer column returns")]
    public void Int_reads_the_int64_of_sqlite() => Assert.Equal(7, ScalarRaw.Of<int>(7L));

    [Fact(DisplayName = "AC6 int reads the Int64 limits of its own range")]
    public void Int_reads_the_limits_of_its_range()
    {
        Assert.Equal(int.MinValue, ScalarRaw.Of<int>((long)int.MinValue));
        Assert.Equal(int.MaxValue, ScalarRaw.Of<int>((long)int.MaxValue));
    }

    [Fact(DisplayName = "AC6 uint reads the Int64 a SQLite integer column returns")]
    public void Uint_reads_the_int64_of_sqlite() => Assert.Equal(4000000000u, ScalarRaw.Of<uint>(4000000000L));

    [Fact(DisplayName = "AC6 ulong reads the Int64 a SQLite integer column returns")]
    public void Ulong_reads_the_int64_of_sqlite() => Assert.Equal(9000000000UL, ScalarRaw.Of<ulong>(9000000000L));

    [Fact(DisplayName = "AC6 ulong reads the largest value its own raw type can hold")]
    public void Ulong_reads_its_largest_value() => Assert.Equal(ulong.MaxValue, ScalarRaw.Of<ulong>(ulong.MaxValue));

    [Fact(DisplayName = "AC6 long reads the Int32 PostgreSQL returns for an int4 column")]
    public void Long_reads_the_int32_of_postgres() => Assert.Equal(7L, ScalarRaw.Of<long>(7));

    [Fact(DisplayName = "AC6 long reads the Int16 PostgreSQL returns for an int2 column")]
    public void Long_reads_the_int16_of_postgres() => Assert.Equal(7L, ScalarRaw.Of<long>((short)7));

    [Fact(DisplayName = "AC6 int reads the Int16 PostgreSQL returns for an int2 column")]
    public void Int_reads_the_int16_of_postgres() => Assert.Equal(7, ScalarRaw.Of<int>((short)7));

    [Fact(DisplayName = "AC6 int reads the Int64 PostgreSQL returns for a bigint column when it fits")]
    public void Int_reads_a_bigint_that_fits() => Assert.Equal(123456, ScalarRaw.Of<int>(123456L));

    [Fact(DisplayName = "AC6 float reads the Int64 of SQLite and the Double of a wider column")]
    public void Float_reads_an_integer_and_a_double()
    {
        Assert.Equal(7f, ScalarRaw.Of<float>(7L));
        Assert.Equal(1.5f, ScalarRaw.Of<float>(1.5));
    }

    [Fact(DisplayName = "AC6 double reads the Int64 of SQLite, the Int32 and Int16 of PostgreSQL and a Single")]
    public void Double_reads_every_integer_shape_and_a_single()
    {
        Assert.Equal(7.0, ScalarRaw.Of<double>(7L));
        Assert.Equal(7.0, ScalarRaw.Of<double>(7));
        Assert.Equal(7.0, ScalarRaw.Of<double>((short)7));
        Assert.Equal(1.5, ScalarRaw.Of<double>(1.5f));
    }

    [Fact(DisplayName = "AC6 decimal reads the Int64 of SQLite and the Int32 of PostgreSQL exactly")]
    public void Decimal_reads_an_integer_exactly()
    {
        Assert.Equal(7m, ScalarRaw.Of<decimal>(7L));
        Assert.Equal(7m, ScalarRaw.Of<decimal>(7));
        Assert.Equal(18446744073709551615m, ScalarRaw.Of<decimal>(ulong.MaxValue));
    }

    [Fact(DisplayName = "AC6 every nullable numeric target reads the integer shapes and keeps the value")]
    public void Nullable_numeric_targets_read_an_integer()
    {
        Assert.Equal(5, ScalarRaw.Of<int?>(5L));
        Assert.Equal(5L, ScalarRaw.Of<long?>(5));
        Assert.Equal(7.0, ScalarRaw.Of<double?>(7L));
        Assert.Equal(7m, ScalarRaw.Of<decimal?>(7L));
        Assert.Equal(5, ScalarRaw.Of<int?>(5));
    }

    [Fact(DisplayName = "AC6 D3 a bool column SQLite hands over as Int64 reads as the integer 1 or 0")]
    public void A_bool_column_read_as_a_number_gives_one_or_zero()
    {
        Assert.Equal(1, ScalarRaw.Of<int>(1L));
        Assert.Equal(0L, ScalarRaw.Of<long>(0L));
        Assert.Equal(1m, ScalarRaw.Of<decimal>(1L));
        Assert.Equal(0.0, ScalarRaw.Of<double>(0L));
    }

    [Fact(DisplayName = "AC4 an Int64 above the range of int is an OverflowException naming Int32 and Int64")]
    public void An_int64_above_int_overflows_by_name()
    {
        var error = ScalarRaw.Overflowed<int>(3000000000L);

        ScalarRaw.Speaks(error, "Int32", "Int64");
    }

    [Fact(DisplayName = "AC4 an Int64 below the range of int and an int? above it are OverflowExceptions")]
    public void An_int64_outside_int_overflows_for_both_ends_and_the_nullable_form()
    {
        Assert.NotNull(ScalarRaw.Overflowed<int>(long.MinValue));
        Assert.NotNull(ScalarRaw.Overflowed<int>(-2147483649L));
        ScalarRaw.Speaks(ScalarRaw.Overflowed<int?>(3000000000L), "Int32?", "Int64");
    }

    [Fact(DisplayName = "AC4 a negative Int64 is an OverflowException for uint, ulong, ushort and byte")]
    public void A_negative_int64_overflows_every_unsigned_target()
    {
        Assert.NotNull(ScalarRaw.Overflowed<uint>(-1L));
        Assert.NotNull(ScalarRaw.Overflowed<ulong>(-1L));
        Assert.NotNull(ScalarRaw.Overflowed<ushort>(-1L));
        Assert.NotNull(ScalarRaw.Overflowed<byte>(-1L));
    }

    [Fact(DisplayName = "AC4 a value one past the range overflows byte, sbyte, short, ushort and uint")]
    public void A_value_one_past_the_range_overflows_every_small_target()
    {
        Assert.NotNull(ScalarRaw.Overflowed<byte>(256L));
        Assert.NotNull(ScalarRaw.Overflowed<sbyte>(128L));
        Assert.NotNull(ScalarRaw.Overflowed<short>(32768L));
        Assert.NotNull(ScalarRaw.Overflowed<ushort>(65536L));
        Assert.NotNull(ScalarRaw.Overflowed<uint>(4294967296L));
    }

    [Fact(DisplayName = "AC4 a ulong above long.MaxValue is an OverflowException for long and for int")]
    public void A_ulong_above_long_overflows()
    {
        Assert.NotNull(ScalarRaw.Overflowed<long>(ulong.MaxValue));
        Assert.NotNull(ScalarRaw.Overflowed<int>(ulong.MaxValue));
    }

    [Fact(DisplayName = "AC4 an integer above the range is an OverflowException, never an InvalidCastException")]
    public void Overflow_is_its_own_exception_type()
    {
        Assert.Throws<OverflowException>(() => ScalarRaw.Of<int>(3000000000L));
        Assert.Equal(3000000000L, ScalarRaw.Of<long>(3000000000L));
    }

    [Fact(DisplayName = "AC4 [SEC] an overflow message never carries the stored number and chains no inner exception")]
    public void An_overflow_message_keeps_the_stored_number_out()
    {
        var error = ScalarRaw.Overflowed<int>(987654321012L);

        ScalarRaw.KeepsQuiet(error, "987654321012");
        ScalarRaw.Speaks(error, "Int32", "Int64");
    }
}
