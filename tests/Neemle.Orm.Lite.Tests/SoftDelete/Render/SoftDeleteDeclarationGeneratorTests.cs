using System.Collections.Immutable;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Render;

public sealed class SoftDeleteDeclarationGeneratorTests
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
            "DeclarationGeneratorFixture",
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

    private static string RegisterLineOf(string generated, string type)
    {
        var marker = "global::Neemle.Orm.Lite.OrmMeta.Register<global::Fixture." + type + ">(";
        var start = generated.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, "no OrmMeta.Register line for " + type + " in the generated source");
        var end = generated.IndexOf('\n', start);
        return generated.Substring(start, end - start).TrimEnd('\r');
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
        var (diagnostics, generated, errors) = Run(@"
[Table(""docs""), SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Equal(
            "global::Neemle.Orm.Lite.OrmMeta.Register<global::Fixture.Doc>(DocOrm.Table, DocOrm.Column, new string[] { \"id\", \"deleted_at\",  }, new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", Platform: true, KeyColumn: \"id\", EntityToken: \"doc\", PropertyColumns: new string[][] { new string[] { \"Id\", \"id\" }, new string[] { \"DeletedAt\", \"deleted_at\" } }));",
            RegisterLineOf(generated, "Doc"));
    }

    [Fact(DisplayName = "[SoftDelete(nameof(X))] overrides the conventional column")]
    public void A_named_column_overrides_the_convention()
    {
        var (diagnostics, generated, errors) = Run(@"
[Table(""docs""), SoftDelete(nameof(RemovedAt), Tenant = SoftDeleteAttribute.Platform)]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? RemovedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"RemovedAt\", Platform: true, KeyColumn: \"id\", EntityToken: \"doc\", PropertyColumns: new string[][] { new string[] { \"Id\", \"id\" }, new string[] { \"RemovedAt\", \"removed_at\" } }));", RegisterLineOf(generated, "Doc"));
    }

    [Fact(DisplayName = "an entity without [SoftDelete] keeps the plain Register line, byte for byte")]
    public void A_plain_entity_keeps_the_plain_register_line()
    {
        var (diagnostics, generated, errors) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Empty(errors);
        Assert.Equal(
            "global::Neemle.Orm.Lite.OrmMeta.Register<global::Fixture.Doc>(DocOrm.Table, DocOrm.Column, new string[] { \"id\", \"deleted_at\",  });",
            RegisterLineOf(generated, "Doc"));
        Assert.DoesNotContain("SoftDeleteMeta", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE002 refuses a [SoftDelete] column that is not a property of the entity")]
    public void A_missing_column_is_refused()
    {
        var (diagnostics, generated, _) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("'Doc'", message, StringComparison.Ordinal);
        Assert.Contains("'DeletedAt'", message, StringComparison.Ordinal);
        Assert.Contains("is not a property of the entity", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SoftDeleteMeta", generated, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "ORMLITE002 refuses a [SoftDelete] column that is not System.DateTime?")]
    [InlineData("DateTime", "System.DateTime")]
    [InlineData("string?", "string?")]
    [InlineData("DateOnly?", "System.DateOnly?")]
    public void A_column_of_another_type_is_refused(string declared, string displayed)
    {
        var (diagnostics, generated, _) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public " + declared + @" DeletedAt { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("is of type '" + displayed + "', not 'System.DateTime?'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SoftDeleteMeta", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE002 refuses a [SoftDelete] column excluded from the column set by [NotAColumn]")]
    public void A_column_excluded_by_NotAColumn_is_refused()
    {
        var (diagnostics, generated, _) = Run(@"
[Table(""docs""), SoftDelete]
public class Doc
{
    [Key] public Guid Id { get; set; }
    [NotAColumn] public DateTime? DeletedAt { get; set; }
}");

        var message = Refusal(diagnostics).GetMessage();
        Assert.Contains("is excluded from the column set", message, StringComparison.Ordinal);
        Assert.DoesNotContain("SoftDeleteMeta", generated, StringComparison.Ordinal);
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
