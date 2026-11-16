using System.Collections.Immutable;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class KeylessEntityGeneratorTests
{
    private const string Header = "using System; using Neemle.Orm.Lite; namespace Fixture;\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated, ImmutableArray<Diagnostic> CompileErrors) Run(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + source);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(SoftDeleteAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Data.Common.DbConnection).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "KeylessGeneratorFixture",
            new[] { tree },
            references.Distinct().ToList(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();

        var generated = string.Join("\n", run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return (run.Diagnostics, generated, errors);
    }

    private const string Keyless = @"
[Table(""pairs"")]
public class Pair
{
    public Guid LeftId { get; set; }
    public string Tag { get; set; } = """";
}";

    private const string Keyed = @"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = """";
}";

    [Fact(DisplayName = "an entity with no [Key] generates source that compiles")]
    public void A_keyless_entity_generates_compilable_source()
    {
        var (diagnostics, _, errors) = Run(Keyless);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
    }

    [Fact(DisplayName = "an entity with no [Key] emits no member that takes a key")]
    public void A_keyless_entity_emits_no_key_member()
    {
        var (_, generated, _) = Run(Keyless);

        Assert.Contains("public static async Task InsertAsync(global::Neemle.Orm.Lite.OrmContext db, global::Fixture.Pair e)", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteAsync", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAsync", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateAsync", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an entity with a [Key] still emits the context and connection forms of Delete and Get")]
    public void A_keyed_entity_still_emits_its_key_members()
    {
        var (diagnostics, generated, errors) = Run(Keyed);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Contains("DeleteAsync(global::Neemle.Orm.Lite.OrmContext db, global::System.Guid key)", generated, StringComparison.Ordinal);
        Assert.Contains("DeleteAsync(DbConnection conn, global::System.Guid key)", generated, StringComparison.Ordinal);
        Assert.Contains("GetAsync(global::Neemle.Orm.Lite.OrmContext db, global::System.Guid key)", generated, StringComparison.Ordinal);
    }
}
