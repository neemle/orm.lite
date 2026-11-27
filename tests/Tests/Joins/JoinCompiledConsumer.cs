using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Neemle.OrmLite.Tests.Joins;

internal static class JoinCompiledConsumer
{
    private static readonly Lazy<Assembly> Consumer = new(Load);

    private static Assembly Load()
    {
        var path = Environment.GetEnvironmentVariable("ORMLITE_JOIN_CONSUMER")
            ?? throw new InvalidOperationException("compiled join consumer path is required");
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }

    internal static IReadOnlyDictionary<string, IJoinColumnBinding> Columns(string entity) =>
        JoinModel.Find(Consumer.Value.GetType(entity, throwOnError: true)!).Snapshot();
}
