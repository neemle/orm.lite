using Xunit;

namespace Neemle.OrmLite.Tests.Joins;

public sealed class JoinLimitsContractTests
{
    [Theory(DisplayName = "[UC-008][AC-114-03] finite positive caps preserve independently chosen capacities up to four sources")]
    [InlineData(1)] [InlineData(4)]
    public void Valid_independent_caps_are_preserved(int sources)
    {
        var limits = new JoinLimits(sources, 1, int.MaxValue, 7, 31);
        Assert.Equal(sources, limits.MaxSources); Assert.Equal(1, limits.MaxProjectionColumns);
        Assert.Equal(int.MaxValue, limits.MaxPredicateNodes); Assert.Equal(7, limits.MaxParameters);
        Assert.Equal(31, limits.MaxResultRows);
    }

    public static IEnumerable<object[]> InvalidCaps()
    {
        for (var index = 0; index < 5; index++)
            foreach (var invalid in new[] { 0, -1 })
            {
                var caps = new[] { 4, 3, 11, 13, 17 }; caps[index] = invalid;
                yield return [caps];
            }
        yield return [new[] { 5, 3, 11, 13, 17 }];
    }

    [Theory(DisplayName = "[UC-008][SEC-114-02] nonpositive caps and the fifth source cannot acquire a valid limits value")]
    [MemberData(nameof(InvalidCaps))]
    public void Each_invalid_capacity_is_refused_without_silent_clamping(int[] caps)
    {
        var failure = Assert.Throws<JoinReadException>(() => new JoinLimits(caps[0], caps[1], caps[2], caps[3], caps[4]));
        Assert.Equal(JoinError.InvalidRequest, failure.Error);
    }
}
