using System.Collections.Immutable;
using Neemle.OrmLite.Generator;
using Neemle.OrmLite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class KeylessEntityGeneratorTests
{
    private const string Header = "using System; using Neemle.OrmLite; namespace Fixture;\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated, ImmutableArray<Diagnostic> CompileErrors, Compilation Output) Run(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + source);

        var compilation = CSharpCompilation.Create(
            "KeylessGeneratorFixture",
            new[] { tree },
            GeneratorReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();

        var generated = string.Join("\n", run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        return (run.Diagnostics, generated, errors, output);
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
        var (diagnostics, _, errors, _) = Run(Keyless);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
    }

    [Fact(DisplayName = "an entity with no [Key] emits no member that takes a key")]
    public void A_keyless_entity_emits_no_key_member()
    {
        var (_, _, _, output) = Run(Keyless);

        var members = GeneratedModel.Of(output).Class("PairOrm").GetMethods().Select(m => m.Name).ToArray();

        Assert.Equal(2, members.Count(n => n == "InsertAsync"));
        Assert.DoesNotContain("DeleteAsync", members);
        Assert.DoesNotContain("GetAsync", members);
        Assert.DoesNotContain("UpdateAsync", members);
    }

    [Fact(DisplayName = "an entity with a [Key] still emits the context and connection forms of Delete and Get")]
    public void A_keyed_entity_still_emits_its_key_members()
    {
        var (diagnostics, _, errors, output) = Run(Keyed);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        var orm = GeneratedModel.Of(output).Class("DocOrm");
        var key = typeof(Guid);
        var context = typeof(OrmContext);
        var connection = typeof(System.Data.Common.DbConnection);
        Assert.NotNull(orm.GetMethod("DeleteAsync", [context, key]));
        Assert.NotNull(orm.GetMethod("DeleteAsync", [context, connection, key]));
        Assert.NotNull(orm.GetMethod("GetAsync", [context, key]));
        Assert.NotNull(orm.GetMethod("GetAsync", [context, connection, key]));
    }
}
