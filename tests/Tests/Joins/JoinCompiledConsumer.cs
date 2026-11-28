using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Neemle.OrmLite.Tests.Joins;

// The consumer is compiled by the actual SDK compiler with the generator as an analyzer, as a package consumer is.
// ORMLITE_JOIN_CONSUMER may name an assembly compiled elsewhere (an external proof harness) instead.
internal static class JoinCompiledConsumer
{
    private static readonly Lazy<Assembly> Consumer = new(Load);

    private static Assembly Load()
    {
        var path = Environment.GetEnvironmentVariable("ORMLITE_JOIN_CONSUMER");
        var assembly = path is null
            ? SdkFixtureCompiler.CompileFixture(JoinConsumerFixture.Source, "JoinConsumer").GetAwaiter().GetResult()
            : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }

    internal static IReadOnlyDictionary<string, IJoinColumnBinding> Columns(string entity) =>
        JoinModel.Find(Consumer.Value.GetType(entity, throwOnError: true)!).Snapshot();
}
