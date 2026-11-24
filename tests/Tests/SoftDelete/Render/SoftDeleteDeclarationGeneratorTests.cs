using System.Collections.Immutable;
using Neemle.OrmLite.Generator;
using Neemle.OrmLite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.Render;

public sealed class SoftDeleteDeclarationGeneratorTests
{
    private const string Header = "using System; using Neemle.OrmLite; namespace Fixture;\n";

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated, ImmutableArray<Diagnostic> CompileErrors, Compilation Output) Run(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(Header + source);

        var compilation = CSharpCompilation.Create(
            "DeclarationGeneratorFixture",
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

    private static Diagnostic Refusal(ImmutableArray<Diagnostic> diagnostics)
    {
        var refusal = Assert.Single(diagnostics.Where(d => d.Id == "ORMLITE002"));
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        return refusal;
    }

    [Fact(DisplayName = "[SoftDelete] by convention registers the DeletedAt property through the soft overload, and the output compiles")]
    public void The_conventional_declaration_registers_DeletedAt_through_the_soft_overload()
    {
        var (diagnostics, generated, errors, output) = Run(@"
[Table(""docs""), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        var meta = GeneratedModel.Of(output).Soft("Doc")!;
        Assert.Equal("DeletedAt", meta.Column);
        Assert.True(meta.Platform);
        Assert.Equal("id", meta.KeyColumn);
        Assert.Equal("doc", meta.EntityToken);
        Assert.Equal(new[] { "Id>id", "DeletedAt>deleted_at" }, meta.PropertyColumns!.Select(p => p[0] + ">" + p[1]));
    }

    [Fact(DisplayName = "[SoftDelete(nameof(X))] overrides the conventional column")]
    public void A_named_column_overrides_the_convention()
    {
        var (diagnostics, generated, errors, output) = Run(@"
[Table(""docs""), SoftDelete(nameof(RemovedAt), Tenant = SoftDeleteAttribute.Platform)]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? RemovedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        var meta = GeneratedModel.Of(output).Soft("Doc")!;
        Assert.Equal("RemovedAt", meta.Column);
        Assert.Equal(new[] { "Id>id", "RemovedAt>removed_at" }, meta.PropertyColumns!.Select(p => p[0] + ">" + p[1]));
    }

    [Fact(DisplayName = "an entity without [SoftDelete] keeps the plain Register line, byte for byte")]
    public void A_plain_entity_keeps_the_plain_register_line()
    {
        var (diagnostics, generated, errors, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        var model = GeneratedModel.Of(output);
        Assert.Null(model.Soft("Doc"));
        Assert.Equal(new[] { "id", "deleted_at" }, model.Descriptor("Doc").ColumnNames);
    }

    [Fact(DisplayName = "ORMLITE002 refuses a [SoftDelete] column that is not a property of the entity")]
    public void A_missing_column_is_refused()
    {
        var (diagnostics, generated, _, output) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("'Doc'", message, StringComparison.Ordinal);
        Assert.Contains("'DeletedAt'", message, StringComparison.Ordinal);
        Assert.Contains("is not a property of the entity", message, StringComparison.Ordinal);
        Assert.Null(GeneratedModel.Of(output).Descriptor("Doc").Soft);
    }

    [Theory(DisplayName = "ORMLITE002 refuses a [SoftDelete] column that is not System.DateTime?")]
    [InlineData("DateTime", "System.DateTime")]
    [InlineData("string?", "string?")]
    [InlineData("DateOnly?", "System.DateOnly?")]
    public void A_column_of_another_type_is_refused(string declared, string displayed)
    {
        var (diagnostics, generated, _, output) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public " + declared + @" DeletedAt { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("is of type '" + displayed + "', not 'System.DateTime?'", message, StringComparison.Ordinal);
        Assert.Null(GeneratedModel.Of(output).Descriptor("Doc").Soft);
    }

    [Fact(DisplayName = "ORMLITE002 refuses a [SoftDelete] column excluded from the column set by [NotAColumn]")]
    public void A_column_excluded_by_NotAColumn_is_refused()
    {
        var (diagnostics, generated, _, output) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
    [NotAColumn] public DateTime? DeletedAt { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("is excluded from the column set", message, StringComparison.Ordinal);
        Assert.Null(GeneratedModel.Of(output).Descriptor("Doc").Soft);
    }

    [Fact(DisplayName = "the three ORMLITE002 cases carry three distinct messages under the one id")]
    public void The_three_cases_carry_distinct_messages()
    {
        var missing = Refusal(Run("[Table(\"docs\"), SoftDelete] public class Doc { [Key] public Guid Id { get; set; } }").Diagnostics).GetMessage();
        var mistyped = Refusal(Run("[Table(\"docs\"), SoftDelete] public class Doc { [Key] public Guid Id { get; set; } public DateTime DeletedAt { get; set; } }").Diagnostics).GetMessage();
        var excluded = Refusal(Run("[Table(\"docs\"), SoftDelete] public class Doc { [Key] public Guid Id { get; set; } [NotAColumn] public DateTime? DeletedAt { get; set; } }").Diagnostics).GetMessage();

        Assert.Equal(3, new[] { missing, mistyped, excluded }.Distinct(StringComparer.Ordinal).Count());
    }
}
