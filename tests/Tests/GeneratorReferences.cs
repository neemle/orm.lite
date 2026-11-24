using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Tests;

// The metadata references every generator test compiles its fixture source against, built ONCE for the process. A
// helper that called MetadataReference.CreateFromFile for every loaded assembly on every run held a fresh native copy
// of about 150 assemblies' metadata per call, and test classes run in parallel; the references are immutable and safe
// to share between compilations, as CorpusGenerator and OrmLiteGoldenTests already do.
internal static class GeneratorReferences
{
    private static readonly Lazy<MetadataReference[]> Shared =
        new(Build, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static IReadOnlyList<MetadataReference> All => Shared.Value;

    private static MetadataReference[] Build()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location);
        var platform = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return loaded.Concat(platform)
            .Distinct(StringComparer.Ordinal)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }
}
