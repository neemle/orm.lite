using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Neemle.OrmLite.Generator;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveSdkCompiler
{
    private static readonly Lazy<Task<Assembly>> Compiled = new(CompileAsync);
    internal static Task<Assembly> Fixture() => Compiled.Value;

    private static async Task<Assembly> CompileAsync()
    {
        var parent = Environment.GetEnvironmentVariable("ORMLITE_SDK_FIXTURE_OUTPUT") ?? Path.GetTempPath();
        var directory = Path.Combine(parent, "partial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "Fixture.cs");
        var output = Path.Combine(directory, "PartialSaveFixture.dll");
        await File.WriteAllTextAsync(source, SavePartialFixture.Source);
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
        var sdk = Directory.GetDirectories(Path.Combine(root, "sdk")).Order(StringComparer.Ordinal).Last();
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
}
