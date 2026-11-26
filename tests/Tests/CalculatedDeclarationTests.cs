using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedDeclarationTests
{
    [Theory(DisplayName = "[UC-001] invalid calculated declarations fail at generation")]
    [InlineData("Missing", "string?", "", "physical text")]
    [InlineData("Name", "int", "", "must be text")]
    [InlineData("Name", "string", "", "nullable source")]
    [InlineData("Name", "string?", "[Key]", "cannot declare")]
    [InlineData("Name", "string?", "[Index]", "cannot declare")]
    [InlineData("Name", "string?", "[Column(\"name\")]", "distinct")]
    [InlineData("Lower", "string?", "", "physical text")]
    [InlineData("Name\"); DROP TABLE notes; --", "string?", "", "physical text")]
    public void Invalid_metadata_is_diagnosed(string source, string type, string roles, string expected)
    {
        var quoted = source.Replace("\"", "\\\"");
        var text = "[Table(\"notes\")] public class Note { public Guid Id { get; set; } "
            + "public string? Name { get; set; } " + roles
            + "[CalculatedLower(\"" + quoted + "\")] public " + type + " Lower { get; set; } }";
        var (diagnostics, _, _) = Run(text);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE023" && d.GetMessage().Contains(expected));
    }

    [Fact(DisplayName = "[UC-001] init-only calculated properties are mapped but excluded from Values")]
    public void Init_only_virtual_property_materializes()
    {
        var (diagnostics, generated, output) = Run("""
            [Table("notes")] public class Note
            {
                public Guid Id { get; set; }
                public string Name { get; set; } = "";
                [CalculatedLower(nameof(Name))] public required string Lower { get; init; }
            }
            """);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "Name"], model.Descriptor("Note").Columns.Select(c => c.Property));
        Assert.Contains("CalculatedLower", generated);
    }
}
