using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class CaseHintProbe
{
    internal static void Run()
    {
        var rows = new[] { "[Neemle.OrmLite.Table(\"aa\")] public class row { public int Id {get;set;} }",
            "[Neemle.OrmLite.Table(\"bb\")] public class Row { public int Id {get;set;} }" };
        var first = Generate(rows); var second = Generate(rows.Reverse());
        if (!first.SequenceEqual(second)) throw new InvalidOperationException("discovery permutation changed output");
        Console.WriteLine(JsonSerializer.Serialize(new { caseDistinctConsumerCompiles = true,
            permutationBytesEqual = true, hints = first.Select(x => x.Split('|')[0]), sourceHashes = first }));
    }

    private static string[] Generate(IEnumerable<string> sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("HintProbe", sources.Select(s => CSharpSyntaxTree.ParseText(s)),
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new Neemle.OrmLite.Generator.OrmGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult();
        var errors = result.Diagnostics.Concat(output.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error);
        if (errors.Any()) throw new InvalidOperationException(string.Join("\n", errors));
        var generated = result.Results.SelectMany(r => r.GeneratedSources).OrderBy(s => s.HintName).ToArray();
        if (generated.Select(s => s.HintName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            throw new InvalidOperationException("case-distinct declaration inventory");
        return generated.Select(s => s.HintName + "|" + Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(s.SourceText.ToString())))).ToArray();
    }
}
