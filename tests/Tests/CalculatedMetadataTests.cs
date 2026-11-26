using System.Reflection;
using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedMetadataTests
{
    private static EntityDescriptor Descriptor() => EntityDescriptor.Of("Note", "notes", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Title", "display_text", ColumnType.Text, true, false);

    [Theory(DisplayName = "[SEC][UC-001] runtime metadata refuses invalid sources and conflicting read aliases")]
    [InlineData("Lower", "lower", "Missing")]
    [InlineData("Lower", "lower", "Id")]
    [InlineData("Lower", "", "Title")]
    [InlineData("Lower", "  ", "Title")]
    [InlineData("Lower", "DISPLAY_TEXT", "Title")]
    [InlineData("Title", "lower", "Title")]
    public void Invalid_runtime_metadata_is_refused(string property, string alias, string source)
    {
        var error = Assert.Throws<ArgumentException>(() => Descriptor().CalculatedLower(property, alias, source));
        Assert.Contains("calculated", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = "[UC-001] virtual aliases and property identities are unique across calculated fields")]
    [InlineData("Other", "LOWER")]
    [InlineData("Lower", "other")]
    public void Repeated_virtual_identity_is_refused(string property, string alias)
    {
        var descriptor = Descriptor().CalculatedLower("Lower", "lower", "Title");
        Assert.Throws<ArgumentException>(() => descriptor.CalculatedLower(property, alias, "Title"));
        Assert.Single(descriptor.Calculated);
    }

    [Fact(DisplayName = "[UC-001] CLR attribute metadata resolves the renamed physical source consistently")]
    public void Attribute_and_generated_mapping_keep_the_declared_source()
    {
        var (diagnostics, _, output) = Run("""
            [Table("notes")] public class Note
            {
                public Guid Id { get; set; }
                [Column("display_text")] public string? Title { get; set; }
                [CalculatedLower(nameof(Title))] public string? Lower { get; set; }
            }
            """);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var type = model.Class("NoteOrm").Assembly.GetType("Fixture.Note", true)!;
        var declaration = type.GetProperty("Lower")!.GetCustomAttribute<CalculatedLowerAttribute>()!;
        Assert.Equal("Title", declaration.SourceProperty);
        var field = Assert.Single(model.Descriptor("Note").Calculated);
        Assert.Equal("display_text", field.SourceColumn);
        Assert.Equal("lower", field.Alias);
    }
}
