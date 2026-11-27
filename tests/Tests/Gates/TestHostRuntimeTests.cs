using System.Runtime.InteropServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

// The test projects target net10.0, so a leg of the CI test job that is meant to run on .NET 11 only does so when the
// test host rolls forward to it. A leg that silently ran on another runtime would test nothing new; the job names its
// leg in ORMLITE_EXPECT_DOTNET with the matrix's SDK version (10.0.x or 11.0.x, as ci.yml passes it) and this test
// fails when the host runs on another major. Only the part before the first dot is compared, so the bare major a
// demo README shows (10, 11) means the same; the orphans demo reads the variable the same way.
public sealed class TestHostRuntimeTests
{
    [Fact(DisplayName = "when ORMLITE_EXPECT_DOTNET names a .NET version, the test host runs on that major")]
    public void The_test_host_runs_on_the_runtime_the_leg_names()
    {
        var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET") ?? "";
        if (expected.Length == 0) return;

        var major = expected.Split('.')[0];
        var actual = Environment.Version.Major.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(actual.Equals(major, StringComparison.Ordinal),
            $"the leg names .NET {expected} but the test host runs on {RuntimeInformation.FrameworkDescription}");
    }
}
