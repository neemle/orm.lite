using Xunit;

namespace Neemle.Orm.Lite.Tests;

internal static class RuntimeSources
{
    private static readonly Lazy<string> Root = new(Locate, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static string LibraryRoot() => Root.Value;

    private static string Locate()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "src", "Neemle.Orm.Lite.Runtime", "Neemle.Orm.Lite.Runtime.csproj")))
                return d.FullName;
        throw new DirectoryNotFoundException("the library root (the folder holding src/Neemle.Orm.Lite.Runtime) was not found above " + AppContext.BaseDirectory);
    }

    internal static string Of(string file)
    {
        var path = Path.Combine(LibraryRoot(), "src", "Neemle.Orm.Lite.Runtime", file);
        Assert.True(File.Exists(path), file + " not found at " + path);
        return path;
    }
}
