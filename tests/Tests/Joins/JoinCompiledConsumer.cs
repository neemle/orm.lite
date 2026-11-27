using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Neemle.OrmLite.Generator;
using Neemle.OrmLite.Tests.GraphSave;

namespace Neemle.OrmLite.Tests.Joins;

// The consumer is compiled by the actual SDK compiler with the generator as an analyzer, as a package consumer is.
// ORMLITE_JOIN_CONSUMER may name an assembly compiled elsewhere (an external proof harness) instead.
internal static class JoinCompiledConsumer
{
    private static readonly Lazy<Assembly> Consumer = new(Load);

    private static Assembly Load()
    {
        var path = Environment.GetEnvironmentVariable("ORMLITE_JOIN_CONSUMER") ?? Compile();
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }

    private static string Compile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "join114-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "JoinConsumer.cs");
        var output = Path.Combine(directory, "JoinConsumer.dll");
        File.WriteAllText(source, JoinConsumerFixture.Source);
        using var process = Process.Start(Start(source, output, directory))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var log = process.StandardError.ReadToEnd() + stdout.Result;
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("join consumer compilation failed:\n" + log);
        return output;
    }

    private static ProcessStartInfo Start(string source, string output, string directory)
    {
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        var root = new DirectoryInfo(runtime).Parent!.Parent!.Parent!.FullName;
        var sdk = SaveSdkCompiler.LatestSdk(Directory.GetDirectories(Path.Combine(root, "sdk")), Environment.Version.Major);
        var composer = Path.Combine(Path.GetDirectoryName(typeof(OrmGenerator).Assembly.Location)!,
            "Neemle.CSharpComposer.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        foreach (var argument in new[] { Path.Combine(sdk, "Roslyn", "bincore", "csc.dll"), "-nologo",
            "-target:library", "-langversion:13.0", "-nullable:enable", "-out:" + output,
            "-generatedfilesout:" + directory, "-analyzer:" + typeof(OrmGenerator).Assembly.Location,
            "-analyzer:" + composer, source })
            start.ArgumentList.Add(argument);
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        foreach (var reference in platform.Append(typeof(OrmContext).Assembly.Location).Distinct())
            start.ArgumentList.Add("-r:" + reference);
        return start;
    }

    internal static IReadOnlyDictionary<string, IJoinColumnBinding> Columns(string entity) =>
        JoinModel.Find(Consumer.Value.GetType(entity, throwOnError: true)!).Snapshot();
}
