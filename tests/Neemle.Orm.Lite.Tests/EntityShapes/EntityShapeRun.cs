using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

internal static class EntityShapeRun
{
    internal const string PropertyId = "ORMLITE023";

    internal const string TypeId = "ORMLITE024";

    internal static ImmutableArray<Diagnostic> CompileErrors(Compilation output) =>
        [.. output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];

    internal static (ImmutableArray<Diagnostic> Diagnostics, string Generated, GeneratedModel Model) Built(
        string source)
    {
        var (diagnostics, generated, output) = Run(source);
        return (diagnostics, generated, GeneratedModel.Of(output));
    }

    internal static string Flat(string text) => Regex.Replace(text, @"\s+", " ");

    internal static string SpanText(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    internal static string MapOf(string generated, string entity)
    {
        var cls = generated.IndexOf("class " + entity + "Orm", StringComparison.Ordinal);
        Assert.True(cls >= 0, "no class for " + entity);
        var start = generated.IndexOf(" Map(", cls, StringComparison.Ordinal);
        var ends = new[] { " Values(", " Column(" }
            .Select(marker => generated.IndexOf(marker, start, StringComparison.Ordinal))
            .Where(index => index > start);
        return Flat(generated[start..ends.Min()]);
    }

    internal static string LoaderOf(string generated, string loader)
    {
        var start = generated.IndexOf(" " + loader + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, "no loader " + loader);
        return Flat(generated[start..generated.IndexOf('}', start)]);
    }

    internal static string[] Methods(GeneratedModel model, string orm) =>
        model.Class(orm)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    internal static string[] Columns(GeneratedModel model, string entity) =>
        model.Descriptor(entity).Columns.Select(c => c.Property).ToArray();

    internal static NotSupportedException MapThrows(GeneratedModel model, string entity)
    {
        var map = model.Class(entity + "Orm").GetMethod("Map")!;
        var failure = Assert.Throws<TargetInvocationException>(() => map.Invoke(null, [null]));
        return Assert.IsType<NotSupportedException>(failure.InnerException);
    }

    internal static void AssertPlainMessage(string message)
    {
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain('\r', message);
        Assert.DoesNotContain('`', message);
        Assert.DoesNotContain('|', message);
        Assert.DoesNotContain('{', message);
        Assert.DoesNotContain('}', message);
    }
}
