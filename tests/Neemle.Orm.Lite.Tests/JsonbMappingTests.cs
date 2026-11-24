using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class JsonbMappingTests
{
    private const string EntityHeader = @"
using System;
using Neemle.Orm.Lite;
namespace Fixture;
";

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated, Compilation Output) Run(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(EntityHeader + source);

        var compilation = CSharpCompilation.Create(
            "JsonbGeneratorFixture",
            new[] { tree },
            GeneratorReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned());
        var run = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _).GetRunResult();

        var generated = string.Join("\n", run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));

        return (run.Diagnostics, generated, output);
    }

    private static string CreateAllOf(Compilation output) =>
        string.Join("; ", GeneratedModel.Render(GeneratedModel.Of(output).Schema.Baseline()));

    [Fact]
    public void A_Jsonb_property_emits_a_jsonb_column_and_not_a_text_one()
    {
        var (diagnostics, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public Jsonb? Payload { get; set; }
}");

        Assert.Empty(diagnostics);

        var createAll = CreateAllOf(output);
        Assert.Contains("\"payload\" jsonb", createAll);
        Assert.DoesNotContain("\"payload\" text", createAll);
    }

    [Fact]
    public void A_non_nullable_Jsonb_column_carries_NOT_NULL_and_an_empty_document_default()
    {
        var (_, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public Jsonb Payload { get; set; } = Jsonb.Parse(""{}"");
}");

        Assert.Contains("\"payload\" jsonb NOT NULL DEFAULT '{}'::jsonb", CreateAllOf(output));
    }

    [Fact]
    public void A_nullable_Jsonb_column_carries_no_default_because_null_is_the_absent_value()
    {
        var (_, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public Jsonb? Payload { get; set; }
}");

        var createAll = CreateAllOf(output);
        Assert.Contains("\"payload\" jsonb", createAll);
        Assert.DoesNotContain("\"payload\" jsonb DEFAULT", createAll);
    }

    [Fact]
    public void The_insert_and_update_placeholders_for_a_jsonb_column_carry_an_explicit_cast()
    {
        var (_, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = """";
    public Jsonb? Payload { get; set; }
}");

        var descriptor = GeneratedModel.Of(output).Descriptor("Doc");
        var insert = GeneratedModel.Text(EntityStatements.Insert(descriptor, new object?[3]));
        var update = GeneratedModel.Text(EntityStatements.Update(descriptor, new object?[3]));

        Assert.Equal("INSERT INTO \"docs\" (\"id\", \"title\", \"payload\") VALUES (@p0, @p1, @p2::jsonb)", insert);
        Assert.Equal("UPDATE \"docs\" SET \"title\" = @s0, \"payload\" = @s1::jsonb WHERE \"id\" = @key", update);
        Assert.DoesNotContain("@p1::jsonb", insert);
    }

    [Fact]
    public void A_jsonb_column_is_read_through_FromStorage_and_bound_through_Json()
    {
        var (_, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public Jsonb? Payload { get; set; }
}");

        Assert.Contains("Jsonb.FromStorage(r.GetString(", generated);
        Assert.Contains("OrmExec.Row(e.Id, e.Payload)", generated);
        using var command = new Microsoft.Data.Sqlite.SqliteCommand();
        Assert.Equal("{\"a\":1}", command.AddParam("x", Jsonb.Parse("{\"a\":1}")).Value);
    }

    [Fact]
    public void An_unmapped_property_type_FAILS_the_build_naming_the_property_and_the_type()
    {
        var (diagnostics, _, _) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public TimeSpan Elapsed { get; set; }
}");

        var refusal = Assert.Single(diagnostics.Where(d => d.Id == "ORMLITE001"));
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);

        var message = refusal.GetMessage();
        Assert.Contains("Doc.Elapsed", message, StringComparison.Ordinal);
        Assert.Contains("System.TimeSpan", message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unmapped_property_is_NOT_emitted_as_a_text_column_which_is_the_fallback_being_closed()
    {
        var (_, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public TimeSpan Elapsed { get; set; }
}");

        var createAll = CreateAllOf(output);
        Assert.DoesNotContain("elapsed", createAll, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"id\" uuid", createAll);
    }

    [Fact]
    public void The_refusal_names_the_ONE_property_that_is_unmapped_and_not_its_mapped_neighbours()
    {
        var (diagnostics, _, _) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public string Title { get; set; } = """";
    public int Rank { get; set; }
    public DateTime CreatedAt { get; set; }
    public Jsonb? Payload { get; set; }
    public TimeSpan Elapsed { get; set; }
}");

        var refusal = Assert.Single(diagnostics.Where(d => d.Id == "ORMLITE001"));
        Assert.Contains("Elapsed", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("string Title { get; set; } = \"\";", "\"title\" text")]
    [InlineData("int Rank { get; set; }", "\"rank\" integer")]
    [InlineData("long Size { get; set; }", "\"size\" bigint")]
    [InlineData("bool Live { get; set; }", "\"live\" boolean")]
    [InlineData("double Ratio { get; set; }", "\"ratio\" double precision")]
    [InlineData("decimal Cost { get; set; }", "\"cost\" numeric(12, 4)")]
    [InlineData("DateTime At { get; set; }", "\"at\" timestamp with time zone")]
    [InlineData("DateOnly Day { get; set; }", "\"day\" date")]
    [InlineData("Guid Owner { get; set; }", "\"owner\" uuid")]
    public void Every_type_that_was_mapped_before_this_ticket_still_maps_to_the_same_SQL_type(
        string declaration, string expectedColumn)
    {
        var (diagnostics, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    public " + declaration + @"
}");

        Assert.Empty(diagnostics);
        Assert.Contains(expectedColumn, CreateAllOf(output));
    }

    [Theory(DisplayName = "a [Numeric] decimal property declares its own precision and scale, in the table and in the descriptor, and an undeclared one keeps 12 and 4")]
    [InlineData("[Numeric(19, 2)]", "\"cost\" numeric(19, 2)", "Nodes.ColumnType.Decimal, false, false, 19, 2")]
    [InlineData("[Numeric(28, 10)]", "\"cost\" numeric(28, 10)", "Nodes.ColumnType.Decimal, false, false, 28, 10")]
    [InlineData("", "\"cost\" numeric(12, 4)", "Nodes.ColumnType.Decimal, false, false)")]
    public void A_numeric_attribute_declares_the_decimal_shape(string attribute, string expectedColumn, string expectedDescriptor)
    {
        var (diagnostics, generated, output) = Run(@"
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
    " + attribute + @" public decimal Cost { get; set; }
}");

        Assert.Empty(diagnostics);
        Assert.Contains(expectedColumn, CreateAllOf(output));
        Assert.Contains(expectedDescriptor, generated, StringComparison.Ordinal);
    }

    [Fact]
    public void A_type_the_COMPILER_could_not_resolve_is_left_to_CS0246_and_is_not_reported_as_unmapped()
    {
        var tree = CSharpSyntaxTree.ParseText(@"
using Neemle.Orm.Lite;
namespace Fixture;
[Table(""docs"")]
public class Doc
{
    [Key] public Guid Id { get; set; }
}");

        var compilation = CSharpCompilation.Create("JsonbUnresolved", new[] { tree }, GeneratorReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Contains(compilation.GetDiagnostics(), d => d.Id == "CS0246");

        var run = CSharpGeneratorDriver
            .Create([new OrmGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGenerators(compilation)
            .GetRunResult();

        Assert.DoesNotContain(run.Diagnostics, d => d.Id == "ORMLITE001");
    }

    [Fact]
    public void The_generator_names_the_jsonb_clr_type_the_runtime_defines()
    {
        Assert.Equal(OrmGenerator.JsonbClrTypeName, typeof(Jsonb).FullName);
    }
}
