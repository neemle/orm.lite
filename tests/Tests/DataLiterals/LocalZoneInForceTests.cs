using Xunit;

namespace Neemle.OrmLite.Tests.DataLiterals;

public sealed class LocalZoneInForceTests
{
    private static readonly string[] UtcNames = ["", "UTC", "Etc/UTC", "GMT", "Z"];

    [Fact(DisplayName = "when the TZ variable names a zone other than UTC, that zone is the local zone of the test "
        + "host, so a Local moment can tell a conversion to UTC from none (a missing zone database falls back to UTC)")]
    public void A_declared_non_utc_zone_is_the_local_zone()
    {
        var declared = Environment.GetEnvironmentVariable("TZ") ?? "";
        if (UtcNames.Contains(declared)) return;

        var moments = new[]
        {
            new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Local),
            new DateTime(2024, 7, 2, 10, 0, 0, DateTimeKind.Local),
        };

        Assert.Contains(moments, moment => TimeZoneInfo.Local.GetUtcOffset(moment) != TimeSpan.Zero);
    }
}
