using System.Linq.Expressions;
using System.Globalization;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class ScalarSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private Task<TCol> Read<TCol>(Guid id, Expression<Func<ScalarProbe, object?>> column) =>
        ScalarWorld.ReadAsync<TCol>(fixture, id, column);

    private Task<ScalarProbe> Mapped(Guid id) => ScalarWorld.MapAsync(fixture, id);

    [SkippableFact(DisplayName = "AC2 ScalarAsync<int> over an int column returns the stored number")]
    public async Task An_int_column_reads_as_int() =>
        Assert.Equal(7, await Read<int>(ScalarWorld.RowA, p => p.Rank));

    [SkippableFact(DisplayName = "AC2 ScalarAsync<long> over an int column returns the stored number as long")]
    public async Task An_int_column_reads_as_long() =>
        Assert.Equal(7L, await Read<long>(ScalarWorld.RowA, p => p.Rank));

    [SkippableFact(DisplayName = "AC2 ScalarAsync<int> over a long column returns the stored number when it fits")]
    public async Task A_long_column_reads_as_int_when_it_fits() =>
        Assert.Equal(123, await Read<int>(ScalarWorld.RowA, p => p.Big));

    [SkippableFact(DisplayName = "AC2 ScalarAsync<double> and <decimal> over an int column return 7")]
    public async Task An_int_column_reads_as_double_and_decimal()
    {
        Assert.Equal(7.0, await Read<double>(ScalarWorld.RowA, p => p.Rank));
        Assert.Equal(7m, await Read<decimal>(ScalarWorld.RowA, p => p.Rank));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<long>, <double> and <string> over their own columns return it")]
    public async Task Columns_read_as_their_own_type()
    {
        Assert.Equal(123L, await Read<long>(ScalarWorld.RowA, p => p.Big));
        Assert.Equal(1.5, await Read<double>(ScalarWorld.RowA, p => p.Ratio));
        Assert.Equal("hello", await Read<string>(ScalarWorld.RowA, p => p.Note));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<decimal> and <decimal?> over a decimal column keep the scale")]
    public async Task A_decimal_column_keeps_its_scale()
    {
        var plain = await Read<decimal>(ScalarWorld.RowA, p => p.Price);
        var optional = await Read<decimal?>(ScalarWorld.RowA, p => p.OptPrice);

        Assert.Equal("12.3400", plain.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("2.5000", optional!.Value.ToString(CultureInfo.InvariantCulture));
        Assert.Null(await Read<decimal?>(ScalarWorld.RowB, p => p.OptPrice));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<bool> returns the stored true and the stored false")]
    public async Task A_bool_column_reads_as_bool()
    {
        Assert.True(await Read<bool>(ScalarWorld.RowA, p => p.Flag));
        Assert.False(await Read<bool>(ScalarWorld.RowB, p => p.Flag));
        Assert.True(await Read<bool?>(ScalarWorld.RowA, p => p.Flag));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<Guid> and <Guid?> return the stored identifier and null")]
    public async Task A_guid_column_reads_as_guid()
    {
        Assert.Equal(ScalarWorld.RowA, await Read<Guid>(ScalarWorld.RowA, p => p.Id));
        Assert.Equal(ScalarWorld.Other, await Read<Guid?>(ScalarWorld.RowA, p => p.OtherId));
        Assert.Null(await Read<Guid?>(ScalarWorld.RowB, p => p.OtherId));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<DateTime> returns the stored instant with Kind Utc")]
    public async Task A_timestamp_column_reads_as_utc()
    {
        var read = await Read<DateTime>(ScalarWorld.RowA, p => p.Stamp);

        Assert.Equal(ScalarWorld.Instant, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<DateTime?> returns the instant with Kind Utc and null for NULL")]
    public async Task A_nullable_timestamp_column_reads_as_utc_or_null()
    {
        var read = await Read<DateTime?>(ScalarWorld.RowA, p => p.OptStamp);

        Assert.Equal(ScalarWorld.OtherInstant, read);
        Assert.Equal(DateTimeKind.Utc, read!.Value.Kind);
        Assert.Null(await Read<DateTime?>(ScalarWorld.RowB, p => p.OptStamp));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<DateOnly> returns the stored day")]
    public async Task A_date_column_reads_as_dateonly() =>
        Assert.Equal(ScalarWorld.Day, await Read<DateOnly>(ScalarWorld.RowA, p => p.Day));

    [SkippableFact(DisplayName = "AC2 ScalarAsync<enum> and <enum?> return the stored member and null")]
    public async Task An_enum_column_reads_as_the_member()
    {
        Assert.Equal(ScalarMood.Bright, await Read<ScalarMood>(ScalarWorld.RowA, p => p.Mood));
        Assert.Equal(ScalarMood.Dim, await Read<ScalarMood?>(ScalarWorld.RowA, p => p.OptMood));
        Assert.Null(await Read<ScalarMood?>(ScalarWorld.RowB, p => p.OptMood));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<int?> returns the stored number and null for NULL")]
    public async Task A_nullable_int_column_reads_the_value_or_null()
    {
        Assert.Equal(5, await Read<int?>(ScalarWorld.RowA, p => p.OptRank));
        Assert.Null(await Read<int?>(ScalarWorld.RowB, p => p.OptRank));
    }

    [SkippableFact(DisplayName = "AC2 ScalarAsync<object?> returns the raw value, Int64 or Int32 by provider")]
    public async Task Object_returns_the_raw_provider_value()
    {
        var raw = await Read<object?>(ScalarWorld.RowA, p => p.Rank);

        Assert.Equal(fixture.ProviderId == "sqlite" ? (object)7L : 7, raw);
        Assert.IsType(fixture.ProviderId == "sqlite" ? typeof(long) : typeof(int), raw);
    }

    [SkippableFact(DisplayName = "AC2 D3 a bool column read as int, long, decimal and double returns 1 on SQLite")]
    public async Task A_bool_column_read_as_a_number_returns_one_on_sqlite()
    {
        Skip.IfNot(fixture.ProviderId == "sqlite", "PostgreSQL hands a boolean over as Boolean.");

        Assert.Equal(1, await Read<int>(ScalarWorld.RowA, p => p.Flag));
        Assert.Equal(1L, await Read<long>(ScalarWorld.RowA, p => p.Flag));
        Assert.Equal(1m, await Read<decimal>(ScalarWorld.RowA, p => p.Flag));
        Assert.Equal(1.0, await Read<double>(ScalarWorld.RowA, p => p.Flag));
    }

    [SkippableFact(DisplayName = "AC3 numbers, flags and text equal the same properties read through the row mapper")]
    public async Task Numbers_flags_and_text_equal_the_row_mapper()
    {
        var mapped = await Mapped(ScalarWorld.RowA);

        Assert.Equal(mapped.Rank, await Read<int>(ScalarWorld.RowA, p => p.Rank));
        Assert.Equal(mapped.Big, await Read<long>(ScalarWorld.RowA, p => p.Big));
        Assert.Equal(mapped.Ratio, await Read<double>(ScalarWorld.RowA, p => p.Ratio));
        Assert.Equal(mapped.Price, await Read<decimal>(ScalarWorld.RowA, p => p.Price));
        Assert.Equal(mapped.OptPrice, await Read<decimal?>(ScalarWorld.RowA, p => p.OptPrice));
        Assert.Equal(mapped.Flag, await Read<bool>(ScalarWorld.RowA, p => p.Flag));
        Assert.Equal(mapped.Note, await Read<string>(ScalarWorld.RowA, p => p.Note));
        Assert.Equal(mapped.OptRank, await Read<int?>(ScalarWorld.RowA, p => p.OptRank));
    }

    [SkippableFact(DisplayName = "AC3 identifiers, instants, days and members equal the row mapper, Kind included")]
    public async Task Identifiers_instants_and_members_equal_the_row_mapper()
    {
        var mapped = await Mapped(ScalarWorld.RowA);
        var stamp = await Read<DateTime>(ScalarWorld.RowA, p => p.Stamp);
        var optStamp = await Read<DateTime?>(ScalarWorld.RowA, p => p.OptStamp);

        Assert.Equal(mapped.Id, await Read<Guid>(ScalarWorld.RowA, p => p.Id));
        Assert.Equal(mapped.OtherId, await Read<Guid?>(ScalarWorld.RowA, p => p.OtherId));
        Assert.Equal(mapped.Stamp, stamp);
        Assert.Equal(mapped.Stamp.Kind, stamp.Kind);
        Assert.Equal(mapped.OptStamp, optStamp);
        Assert.Equal(mapped.OptStamp!.Value.Kind, optStamp!.Value.Kind);
        Assert.Equal(mapped.Day, await Read<DateOnly>(ScalarWorld.RowA, p => p.Day));
        Assert.Equal(mapped.Mood, await Read<ScalarMood>(ScalarWorld.RowA, p => p.Mood));
        Assert.Equal(mapped.OptMood, await Read<ScalarMood?>(ScalarWorld.RowA, p => p.OptMood));
    }

    [SkippableFact(DisplayName = "AC3 the row of NULLs reads as the same nulls through ScalarAsync and the row mapper")]
    public async Task Nulls_equal_the_row_mapper()
    {
        var mapped = await Mapped(ScalarWorld.RowB);

        Assert.Equal(mapped.OptPrice, await Read<decimal?>(ScalarWorld.RowB, p => p.OptPrice));
        Assert.Equal(mapped.OtherId, await Read<Guid?>(ScalarWorld.RowB, p => p.OtherId));
        Assert.Equal(mapped.OptStamp, await Read<DateTime?>(ScalarWorld.RowB, p => p.OptStamp));
        Assert.Equal(mapped.OptMood, await Read<ScalarMood?>(ScalarWorld.RowB, p => p.OptMood));
        Assert.Equal(mapped.OptRank, await Read<int?>(ScalarWorld.RowB, p => p.OptRank));
    }

    [SkippableFact(DisplayName = "AC5 no matching row returns default for every target class")]
    public async Task No_row_reads_as_default()
    {
        var none = ScalarWorld.Missing;

        Assert.Equal(0, await Read<int>(none, p => p.Rank));
        Assert.Equal(0m, await Read<decimal>(none, p => p.Price));
        Assert.False(await Read<bool>(none, p => p.Flag));
        Assert.Equal(Guid.Empty, await Read<Guid>(none, p => p.Id));
        Assert.Equal(default(DateTime), await Read<DateTime>(none, p => p.Stamp));
        Assert.Equal(ScalarMood.Calm, await Read<ScalarMood>(none, p => p.Mood));
        Assert.Null(await Read<string>(none, p => p.Note));
    }

    [SkippableFact(DisplayName = "AC5 no matching row returns null for every nullable target")]
    public async Task No_row_reads_as_null_for_nullable_targets()
    {
        var none = ScalarWorld.Missing;

        Assert.Null(await Read<int?>(none, p => p.OptRank));
        Assert.Null(await Read<Guid?>(none, p => p.OtherId));
        Assert.Null(await Read<DateTime?>(none, p => p.OptStamp));
        Assert.Null(await Read<ScalarMood?>(none, p => p.OptMood));
        Assert.Null(await Read<decimal?>(none, p => p.OptPrice));
    }

    [SkippableFact(DisplayName = "AC5 SQL NULL read into a non-nullable target returns default, as no row does")]
    public async Task Sql_null_reads_as_default_for_a_plain_target()
    {
        Assert.Equal(0, await Read<int>(ScalarWorld.RowB, p => p.OptRank));
        Assert.Equal(0m, await Read<decimal>(ScalarWorld.RowB, p => p.OptPrice));
        Assert.Equal(Guid.Empty, await Read<Guid>(ScalarWorld.RowB, p => p.OtherId));
        Assert.Equal(default(DateTime), await Read<DateTime>(ScalarWorld.RowB, p => p.OptStamp));
        Assert.Equal(ScalarMood.Calm, await Read<ScalarMood>(ScalarWorld.RowB, p => p.OptMood));
    }
}
