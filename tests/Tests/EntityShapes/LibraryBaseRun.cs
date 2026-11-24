using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Neemle.OrmLite.Tests.EntityShapes;

internal static class LibraryBaseRun
{
    internal const string UserAssembly = "LibraryUserFixture";

    private const string Header = "using System; using Neemle.OrmLite; namespace Fixture;\n";

    private static int libraryCounter;

    internal static (ImmutableArray<Diagnostic> Diagnostics, Compilation Output) Run(string library, string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        references.Add(MetadataReference.CreateFromImage(Library(library, references)));
        var compilation = CSharpCompilation.Create(
            UserAssembly,
            new[] { CSharpSyntaxTree.ParseText(Header + source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver
            .Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Diagnostics, output);
    }

    internal static string TextOf(Compilation output) =>
        string.Join("\n", output.SyntaxTrees.Select(tree => tree.ToString()));

    private static ImmutableArray<byte> Library(string source, List<MetadataReference> references)
    {
        var library = CSharpCompilation.Create(
            "LibraryFixture" + Interlocked.Increment(ref libraryCounter),
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emitted = library.Emit(stream);
        if (!emitted.Success)
            throw new InvalidOperationException(string.Join("\n", emitted.Diagnostics));
        return stream.ToArray().ToImmutableArray();
    }
}
