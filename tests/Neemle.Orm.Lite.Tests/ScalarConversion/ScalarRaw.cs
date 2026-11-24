using Xunit;

namespace Neemle.Orm.Lite.Tests.ScalarConversion;

internal static class ScalarRaw
{
    internal const string Table = "scalar_probe_rows";

    internal const string Column = "probe_column";

    internal static T Of<T>(object? raw) => ScalarValue.To<T>(raw, Table, Column);

    internal static InvalidCastException Refused<T>(object raw) =>
        Assert.Throws<InvalidCastException>(() => Of<T>(raw));

    internal static OverflowException Overflowed<T>(object raw) =>
        Assert.Throws<OverflowException>(() => Of<T>(raw));

    internal static void Speaks(Exception error, string asked, string source)
    {
        Assert.StartsWith("ScalarAsync<" + asked + ">", error.Message);
        Assert.Contains(source, error.Message);
        Assert.Null(error.InnerException);
    }

    internal static void KeepsQuiet(Exception error, string stored)
    {
        Assert.DoesNotContain(stored, error.Message);
        Assert.Null(error.InnerException);
    }
}
