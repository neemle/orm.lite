using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Neemle.OrmLite.Generator;
using Xunit;

namespace Neemle.OrmLite.Tests;

// Compiles a fixture source with the actual SDK compiler, the generator and Composer as analyzers, as a package
// consumer is compiled, and loads the assembly. Shared by the GraphSave, Orphans and Joins fixtures. The compiler log
// and argv are kept beside the output (ORMLITE_SDK_FIXTURE_OUTPUT names another parent than the temp directory).
internal static class SdkFixtureCompiler
{
    internal static async Task<Assembly> CompileFixture(string sourceText, string assemblyName)
    {
        var parent = Environment.GetEnvironmentVariable("ORMLITE_SDK_FIXTURE_OUTPUT") ?? Path.GetTempPath();
        var directory = Path.Combine(parent, "sdk-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "Fixture.cs");
        var output = Path.Combine(directory, assemblyName + ".dll");
        await File.WriteAllTextAsync(source, sourceText);
        var start = Start(source, output, directory);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var log = await stdout + await stderr;
        await File.WriteAllTextAsync(Path.Combine(directory, "compiler.log"), log);
        await File.WriteAllTextAsync(Path.Combine(directory, "compiler.argv"),
            string.Join("\n", start.ArgumentList));
        Assert.True(process.ExitCode == 0, "Actual SDK fixture compilation failed:\n" + log);
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(output);
    }

    private static ProcessStartInfo Start(string source, string output, string directory)
    {
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var root = new DirectoryInfo(runtime).Parent!.Parent!.Parent!.FullName;
        var sdk = LatestSdk(Directory.GetDirectories(Path.Combine(root, "sdk")), Environment.Version.Major);
        var compiler = Path.Combine(sdk, "Roslyn", "bincore", "csc.dll");
        var composer = Path.Combine(Path.GetDirectoryName(typeof(OrmGenerator).Assembly.Location)!,
            "Neemle.CSharpComposer.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        foreach (var argument in new[] { compiler, "-nologo", "-target:library", "-langversion:13.0",
            "-nullable:enable", "-out:" + output, "-generatedfilesout:" + directory,
            "-analyzer:" + typeof(OrmGenerator).Assembly.Location, "-analyzer:" + composer, source })
            start.ArgumentList.Add(argument);
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var reference in platform.Append(typeof(OrmContext).Assembly.Location).Distinct())
            start.ArgumentList.Add("-r:" + reference);
        return start;
    }

    internal static string LatestSdk(IEnumerable<string> directories, int runtimeMajor) => directories
        .Select(path => (Path: path, Version: Version.TryParse(Path.GetFileName(path).Split('-')[0],
            out var version) ? version : null))
        .Where(sdk => sdk.Version is not null && sdk.Version.Major >= runtimeMajor)
        .OrderBy(sdk => sdk.Version)
        .Select(sdk => sdk.Path)
        .Last();
}
