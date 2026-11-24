using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

internal sealed class GeneratedModel
{
    private const string GeneratedNamespace = "Neemle.Orm.Lite.Generated.";

    private readonly Assembly _assembly;

    private readonly string _token;

    private GeneratedModel(Assembly assembly, string token)
    {
        _assembly = assembly;
        _token = token;
    }

    internal static GeneratedModel Of(Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
        return new GeneratedModel(assembly, output.AssemblyName!);
    }

    internal string Token => _token;

    internal void RunModuleInitializer() =>
        System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(_assembly.ManifestModule.ModuleHandle);

    internal OrmSchema Schema =>
        (OrmSchema)_assembly.GetType(GeneratedNamespace + _token + "Schema", throwOnError: true)!.GetField("Instance")!.GetValue(null)!;

    internal IReadOnlyList<MigrationContribution> Contributions =>
        (MigrationContribution[])_assembly.GetType(GeneratedNamespace + _token + "Migrations", throwOnError: true)!.GetField("Contributions")!.GetValue(null)!;

    internal EntityDescriptor Descriptor(string type) =>
        (EntityDescriptor)_assembly.GetType(GeneratedNamespace + type + "Orm", throwOnError: true)!.GetField("Descriptor")!.GetValue(null)!;

    internal Type Class(string name) => _assembly.GetType(GeneratedNamespace + name, throwOnError: true)!;

    internal bool Has(string name) => _assembly.GetType(GeneratedNamespace + name) is not null;

    internal SoftDeleteMeta? Soft(string type) => Descriptor(type).ToSoftMeta();

    internal static IReadOnlyList<string> Render(IEnumerable<global::Neemle.Orm.Lite.Nodes.Node> plan) => DdlCompiler.Render(plan, PostgresProvider.Instance);

    internal static string Text(global::Neemle.Orm.Lite.Nodes.Node statement) =>
        StatementCompiler.Compile(statement, PostgresProvider.Instance, StatementKind.Write).Text;
}
