using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

public sealed class SaveSdkSelectionTests
{
    [Theory(DisplayName = "SDK fixture compiler uses a numerically current compatible SDK regardless of folder order")]
    [InlineData(10, "10.0.404", "8.0.428,10.0.303,9.0.305,10.0.404")]
    [InlineData(10, "11.0.100", "11.0.100,8.0.428,10.0.404")]
    [InlineData(11, "11.0.101", "11.0.101,9.0.305,11.0.100,not-an-sdk")]
    public void Compatible_sdk_versions_are_ordered_numerically(int major, string expected, string names)
    {
        var paths = names.Split(',').Select(name => Path.Combine("sdk", name)).ToArray();
        Assert.Equal(Path.Combine("sdk", expected), SdkFixtureCompiler.LatestSdk(paths, major));
        Assert.Equal(Path.Combine("sdk", expected), SdkFixtureCompiler.LatestSdk(paths.Reverse(), major));
    }

    [Fact(DisplayName = "SDK fixture compiler refuses an installation with only older runtimes")]
    public void An_incompatible_sdk_is_not_used_as_a_fallback() =>
        Assert.Throws<InvalidOperationException>(() => SdkFixtureCompiler.LatestSdk(["8.0.428", "9.0.305"], 10));
}
