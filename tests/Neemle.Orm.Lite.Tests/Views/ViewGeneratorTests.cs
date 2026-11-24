using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Views;

public sealed class ViewGeneratorTests
{
    private const string Header = "using System; using Neemle.Orm.Lite; namespace Fixture;\n";

    private const string View = @"
[View(""open_work"")]
public class OpenWork
{
    public Guid Id { get; set; }
    public string Title { get; set; } = """";
    public int? Points { get; set; }
}";

    private static (ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<Diagnostic> CompileErrors, Compilation Output) Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "ViewGeneratorFixture",
            [CSharpSyntaxTree.ParseText(Header + source)],
            GeneratorReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult().Diagnostics, output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray(), output);
    }

    [Fact(DisplayName = "a [View] class generates a read-only repository: the mapping, the descriptor flagged as a view and a query, and no value row or key lookup")]
    public void A_view_generates_a_read_only_repository()
    {
        var (diagnostics, errors, output) = Run(View);

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        var model = GeneratedModel.Of(output);
        var orm = model.Class("OpenWorkOrm");
        var names = orm.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).Distinct().Order().ToArray();
        Assert.Equal(["Column", "DeleteAsync", "InsertAsync", "Map", "Query", "UpdateAsync"], names);
        var descriptor = model.Descriptor("OpenWork");
        Assert.True(descriptor.IsView);
        Assert.False(descriptor.Managed);
        Assert.Equal("open_work", descriptor.Table);
        Assert.Equal(["id", "title", "points"], descriptor.ColumnNames);
    }

    [Fact(DisplayName = "the write members of a view repository are errors at the call site and name the view")]
    public void The_write_members_of_a_view_are_obsolete_errors()
    {
        var (_, _, output) = Run(View);

        var orm = GeneratedModel.Of(output).Class("OpenWorkOrm");

        foreach (var name in new[] { "InsertAsync", "UpdateAsync", "DeleteAsync" })
        {
            var obsolete = orm.GetMethod(name)!.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(obsolete);
            Assert.True(obsolete.IsError);
            Assert.Contains("open_work is a view", obsolete.Message, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "a call of a write member on a view repository does not compile, whichever arguments it passes")]
    public void A_write_call_on_a_view_does_not_compile()
    {
        var (_, errors, _) = Run(View + @"
public static class Caller
{
    public static System.Threading.Tasks.Task Go(OrmContext db, OpenWork w) =>
        Neemle.Orm.Lite.Generated.OpenWorkOrm.InsertAsync(db, w);
}");

        var refusal = Assert.Single(errors);
        Assert.Equal("CS0619", refusal.Id);
        Assert.Contains("open_work is a view", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a view is not part of the generated baseline, so no table is created for it")]
    public void A_view_has_no_baseline_table()
    {
        var (_, _, output) = Run(View + @"
[Table(""notes"")] public class Note { public Guid Id { get; set; } }");

        var schema = GeneratedModel.Of(output).Schema;

        Assert.Equal(["notes"], GeneratedModel.Render(schema.Baseline()).Select(s => s.Split('"')[1]));
        Assert.Equal(2, schema.Entities.Count);
    }
}
