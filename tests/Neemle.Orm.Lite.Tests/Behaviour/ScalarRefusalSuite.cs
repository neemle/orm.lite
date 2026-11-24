using System.Linq.Expressions;
using Neemle.Orm.Lite.Tests.ScalarConversion;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class ScalarRefusalSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private string IntegerRaw => Keyed("Int64", "Int32");

    private string DecimalRaw => Keyed("String", "Decimal");

    private string StampRaw => Keyed("String", "DateTime");

    private string Keyed(string sqlite, string postgres) => fixture.ProviderId switch
    {
        "sqlite" => sqlite,
        "postgres" => postgres,
        _ => throw new InvalidOperationException("no raw type is written down for provider " + fixture.ProviderId),
    };

    private Task<TError> Raised<TError, TCol>(Guid id, Expression<Func<ScalarProbe, object?>> column)
        where TError : Exception =>
        Assert.ThrowsAsync<TError>(() => ScalarWorld.ReadAsync<TCol>(fixture, id, column));

    private Task<InvalidCastException> Refused<TCol>(Guid id, Expression<Func<ScalarProbe, object?>> column) =>
        Raised<InvalidCastException, TCol>(id, column);

    [SkippableFact(DisplayName = "AC4 ScalarAsync<int> over a long above Int32.MaxValue is an OverflowException")]
    public async Task A_long_above_int_overflows_by_name()
    {
        var error = await Raised<OverflowException, int>(ScalarWorld.Numbers, p => p.Big);

        ScalarRaw.Speaks(error, "Int32", "Int64");
        ScalarRaw.KeepsQuiet(error, "3000000000");
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<int> over text and <string> over an int column are refused")]
    public async Task Text_is_not_an_int_and_an_int_is_not_text()
    {
        var text = await Refused<int>(ScalarWorld.NotGuid, p => p.Note);
        var number = await Refused<string>(ScalarWorld.RowA, p => p.Rank);

        ScalarRaw.Speaks(text, "Int32", "String");
        ScalarRaw.KeepsQuiet(text, "not-a-guid");
        ScalarRaw.Speaks(number, "String", IntegerRaw);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<Guid> over text that is not a Guid is refused without the text")]
    public async Task Text_that_is_not_a_guid_is_refused()
    {
        var error = await Refused<Guid>(ScalarWorld.NotGuid, p => p.Note);

        ScalarRaw.Speaks(error, "Guid", "String");
        ScalarRaw.KeepsQuiet(error, "not-a-guid");
        Assert.Contains("the text is not a Guid in the form the library writes", error.Message);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<bool> over the integer 2 is refused and says why")]
    public async Task An_integer_other_than_zero_and_one_is_not_a_bool()
    {
        var error = await Refused<bool>(ScalarWorld.Numbers, p => p.Rank);

        ScalarRaw.Speaks(error, "Boolean", IntegerRaw);
        Assert.Contains("only the integers 0 and 1 are a bool", error.Message);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<decimal> over a double column is refused and says why")]
    public async Task A_double_column_is_not_read_as_decimal()
    {
        var error = await Refused<decimal>(ScalarWorld.Numbers, p => p.Ratio);

        ScalarRaw.Speaks(error, "Decimal", "Double");
        Assert.Contains("rounded through a floating point number", error.Message);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<int> and <double> over a decimal column are refused")]
    public async Task A_decimal_column_is_not_read_as_int_or_double()
    {
        var asInt = await Refused<int>(ScalarWorld.RowA, p => p.Price);
        var asDouble = await Refused<double>(ScalarWorld.RowA, p => p.Price);

        ScalarRaw.Speaks(asInt, "Int32", DecimalRaw);
        ScalarRaw.Speaks(asDouble, "Double", DecimalRaw);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<DateTimeOffset> over a timestamp is refused, generic reason")]
    public async Task A_timestamp_is_not_read_as_datetimeoffset()
    {
        var error = await Refused<DateTimeOffset>(ScalarWorld.RowA, p => p.Stamp);

        ScalarRaw.Speaks(error, "DateTimeOffset", StampRaw);
        Assert.Contains("no conversion between these types is defined", error.Message);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<enum> over text that is no member is an OrmEnumValueException")]
    public async Task Text_that_is_no_member_is_an_enum_value_exception()
    {
        var error = await Raised<OrmEnumValueException, ScalarMood>(ScalarWorld.Zeta, p => p.Note);

        Assert.Equal(ScalarProbe.Table, error.Table);
        Assert.Equal("note", error.Column);
        Assert.Equal("Zeta", error.Value);
        Assert.Equal(["Calm", "Bright", "Dim"], error.Accepted);
    }

    [SkippableFact(DisplayName = "AC4 [SEC] ScalarAsync<enum> over stored text 7 and 1 is never a member")]
    public async Task Stored_numbers_as_text_are_not_enum_members()
    {
        var seven = await Raised<OrmEnumValueException, ScalarMood>(ScalarWorld.Seven, p => p.Note);
        var one = await Raised<OrmEnumValueException, ScalarMood>(ScalarWorld.One, p => p.Note);

        Assert.Equal("7", seven.Value);
        Assert.Equal("1", one.Value);
    }

    [SkippableFact(DisplayName = "AC4 ScalarAsync<enum> over a number is refused by name")]
    public async Task A_number_is_not_read_as_an_enum()
    {
        var error = await Refused<ScalarMood>(ScalarWorld.RowA, p => p.Rank);

        ScalarRaw.Speaks(error, "ScalarMood", IntegerRaw);
    }

    [SkippableFact(DisplayName = "AC4 a refused read leaves the rows as they were")]
    public async Task Refused_reads_change_no_row()
    {
        await Raised<OverflowException, int>(ScalarWorld.Numbers, p => p.Big);
        var after = await ScalarWorld.MapAsync(fixture, ScalarWorld.Numbers);

        Assert.Equal(3000000000L, after.Big);
        Assert.Equal(2, after.Rank);
        Assert.Equal("abc", after.Note);
    }
}
