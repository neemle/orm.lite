using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

public sealed class JoinCellContractTests
{
    [Fact(DisplayName = "[UC-008][AC-114-02] a default reference cell is absent and cannot yield a value")]
    public void Default_reference_cell_is_absent() => Missing(default(JoinCell<string>), false);

    [Fact(DisplayName = "[UC-008][AC-114-02] a default nonnullable value cell does not fabricate zero")]
    public void Default_nonnullable_cell_is_absent() => Missing(default(JoinCell<int>), false);

    [Fact(DisplayName = "[UC-008][AC-114-02] a default nullable cell does not return a successful null")]
    public void Default_nullable_cell_is_absent() => Missing(default(JoinCell<int?>), false);

    [Theory(DisplayName = "[UC-008][AC-114-02] absent and matched SQL NULL cells have different presence but no payload")]
    [InlineData(false)] [InlineData(true)]
    public void Null_and_absence_remain_distinct_for_every_value_shape(bool present)
    {
        Missing(new JoinCell<string>(present, true, "unavailable payload"), present);
        Missing(new JoinCell<int>(present, true, 321), present);
        Missing(new JoinCell<int?>(present, true, 654), present);
    }

    [Fact(DisplayName = "[UC-008][AC-114-07] present false, zero and empty scalar values remain actual values")]
    public void Present_scalars_are_not_confused_with_missing_defaults()
    {
        Present(false); Present(0); Present(0L); Present(""); Present<int?>(0);
        Present(Guid.Empty); Present(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory(DisplayName = "[UC-008][AC-114-02] contradictory decoder presence cannot create a usable payload")]
    [InlineData(false)] [InlineData(true)]
    public void Invalid_decoder_states_fail_mapping_instead_of_exposing_a_value(bool nullPayload)
    {
        var failure = nullPayload
            ? Assert.Throws<JoinReadException>(() => new JoinCell<string>(true, false, null!))
            : Assert.Throws<JoinReadException>(() => new JoinCell<string>(false, false, "private payload"));
        Assert.Equal(JoinError.MappingFailure, failure.Error);
        Assert.DoesNotContain("private payload", failure.ToString(), StringComparison.Ordinal);
    }

    private static void Missing<T>(JoinCell<T> cell, bool present)
    {
        Assert.Equal(present, cell.SourcePresent);
        Assert.True(cell.IsNull);
        Assert.Equal(JoinError.MissingValue, Assert.Throws<JoinReadException>(() => cell.Value).Error);
    }

    private static void Present<T>(T expected)
    {
        var cell = new JoinCell<T>(true, false, expected);
        Assert.True(cell.SourcePresent); Assert.False(cell.IsNull); Assert.Equal(expected, cell.Value);
    }
}
