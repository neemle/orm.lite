using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedAccessTests
{
    [Theory(DisplayName = "[UC-001] inaccessible calculated targets and empty or repeated aliases are diagnosed")]
    [InlineData("[CalculatedLower(nameof(Name))] public string? Lower { get; }", "assignable")]
    [InlineData("[CalculatedLower(nameof(Name))] public string? Lower { get; private set; }", "private")]
    [InlineData("[CalculatedLower(null)] public string? Lower { get; set; }", "physical text")]
    [InlineData("[Column(\" \")][CalculatedLower(nameof(Name))] public string? Lower { get; set; }", "distinct")]
    [InlineData("[CalculatedLower(nameof(Name))] public string? Lower { get; set; } "
        + "[Column(\"lower\")][CalculatedLower(nameof(Name))] public string? Other { get; set; }", "distinct")]
    public void Invalid_declarations_have_specific_diagnostics(string members, string expected)
    {
        var source = "[Table(\"notes\")] public class Note { public Guid Id { get; set; } "
            + "public string? Name { get; set; } " + members + " }";
        var (diagnostics, _, _) = Run(source);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE023" && d.GetMessage().Contains(expected));
    }

    [Fact(DisplayName = "[UC-001] independent calculated fields may share one physical source")]
    public void Multiple_virtual_fields_keep_distinct_read_aliases()
    {
        var (diagnostics, _, output) = Run("""
            [Table("notes")] public class Note
            {
                public Guid Id { get; set; }
                public string? Name { get; set; }
                [CalculatedLower(nameof(Name))] public string? Lower { get; set; }
                [CalculatedLower(nameof(Name))] public string? Other { get; set; }
            }
            """);
        Assert.Empty(diagnostics);
        var fields = GeneratedModel.Of(output).Descriptor("Note").Calculated;
        Assert.Equal(["lower", "other"], fields.Select(f => f.Alias));
    }
    [Fact(DisplayName = "[UC-001] explicit join entities retain calculated target loaders without pure-link writers")]
    public void Declared_join_entity_keeps_the_typed_read_loader()
    {
        var (diagnostics, _, output) = Run("""
            [Table("owners")] public class Owner
            {
                public Guid Id { get; set; }
                [ManyToMany("owner_notes", "owner_id", "note_id")]
                public System.Collections.Generic.List<Note> Notes { get; set; } = new();
            }
            [Table("notes")] public class Note
            {
                public Guid Id { get; set; }
                public string? Name { get; set; }
                [CalculatedLower(nameof(Name))] public string? Lower { get; set; }
            }
            [Table("owner_notes")] public class OwnerNote
            {
                public Guid OwnerId { get; set; }
                public Guid NoteId { get; set; }
            }
            """);
        Assert.Empty(diagnostics);
        var methods = GeneratedModel.Of(output).Class("OwnerOrm").GetMethods().Select(m => m.Name);
        Assert.Contains("LoadNotesAsync", methods);
        Assert.DoesNotContain("AddNoteAsync", methods);
    }
}
