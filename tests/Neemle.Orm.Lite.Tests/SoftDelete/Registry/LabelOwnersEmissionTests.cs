using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Registry;

public sealed class LabelOwnersEmissionTests
{
    private const string Members =
        "[Key] public Guid Id { get; set; } public string Title { get; set; } = \"\"; public Guid ProjectId { get; set; }"
        + " public Guid? ParentId { get; set; } public int Rank { get; set; } public DateTime? DeletedAt { get; set; }";

    private static string Source(string type, string arguments, string members = Members) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform{arguments})] public class {type} {{ {members} }}\n";

    private static SoftDeleteMeta Meta(string type, string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return GeneratedModel.Of(output).Soft(type)!;
    }

    private static readonly string[] Columns = ["Id>id", "Title>title", "ProjectId>project_id", "ParentId>parent_id", "Rank>rank", "DeletedAt>deleted_at"];

    private static Diagnostic Refusal(string id, string source)
    {
        var (diagnostics, _, _) = Run(source);
        return Assert.Single(diagnostics.Where(d => d.Id == id));
    }

    [Fact(DisplayName = "a Label and Owners are registered as exactly those database columns, in declaration order, after the key column")]
    public void Label_and_owners_are_emitted_as_database_columns()
    {
        var meta = Meta("Doc", Source("Doc", ", Label = nameof(Title), Owners = new[] { nameof(ProjectId), nameof(ParentId) }"));

        Assert.True(meta.Platform);
        Assert.Equal("id", meta.KeyColumn);
        Assert.Equal("title", meta.LabelColumn);
        Assert.Equal(new[] { "project_id", "parent_id" }, meta.Owners);
        Assert.Equal("doc", meta.EntityToken);
        Assert.Equal(Columns, meta.PropertyColumns!.Select(p => p[0] + ">" + p[1]));
    }

    [Fact(DisplayName = "a Label and an Owner with an explicit [Column] name are registered under the database column name")]
    public void Explicit_column_names_are_emitted()
    {
        var meta = Meta("Doc", Source("Doc", ", Label = nameof(Title), Owners = new[] { nameof(ProjectId) }",
            "[Key] public Guid Id { get; set; } [Column(\"heading\")] public string Title { get; set; } = \"\"; [Column(\"proj\")] public Guid ProjectId { get; set; } public DateTime? DeletedAt { get; set; }"));

        Assert.Equal("heading", meta.LabelColumn);
        Assert.Equal(new[] { "proj" }, meta.Owners);
    }

    [Fact(DisplayName = "an owner named twice is registered once")]
    public void A_repeated_owner_is_emitted_once()
    {
        var meta = Meta("Doc", Source("Doc", ", Owners = new[] { nameof(ProjectId), nameof(ProjectId) }"));

        Assert.Equal(new[] { "project_id" }, meta.Owners);
    }

    [Fact(DisplayName = "a soft entity declaring neither Label nor Owners registers neither argument")]
    public void Undeclared_label_and_owners_emit_no_argument()
    {
        var meta = Meta("Doc", Source("Doc", ""));

        Assert.Null(meta.LabelColumn);
        Assert.Null(meta.Owners);
        Assert.Equal("doc", meta.EntityToken);
    }

    [Theory(DisplayName = "the entity token is the snake_case of the CLR type name")]
    [InlineData("WorkItem", "work_item")]
    [InlineData("SpecNode", "spec_node")]
    [InlineData("Project", "project")]
    [InlineData("VaultEntry", "vault_entry")]
    [InlineData("Page2Item", "page2_item")]
    public void The_entity_token_is_snake_case(string type, string token)
    {
        var meta = Meta(type, Source(type, ""));

        Assert.Equal(token, meta.EntityToken);
    }

    [Fact(DisplayName = "a non-soft entity registers no entity token and no property map")]
    public void A_plain_entity_registers_no_soft_metadata()
    {
        var (diagnostics, _, output) = Run("[Table(\"docs\")] public class Doc { " + Members + " }\n");
        Assert.Empty(diagnostics);

        var model = GeneratedModel.Of(output);

        Assert.Null(model.Soft("Doc"));
        Assert.Null(model.Descriptor("Doc").Soft);
    }

    [Fact(DisplayName = "ORMLITE010 refuses a Label that is not a mapped property and names the entity and the name")]
    public void An_unmapped_label_is_refused()
    {
        var refusal = Refusal("ORMLITE010", Source("Doc", ", Label = \"Heading\""));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Heading'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Label", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE010 refuses an Owners name that is not a mapped property and names the entity and the name")]
    public void An_unmapped_owner_is_refused()
    {
        var refusal = Refusal("ORMLITE010", Source("Doc", ", Owners = new[] { nameof(ProjectId), \"Orphan\" }"));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Orphan'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Owners", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE011 refuses a Label that is not a text column and names the entity and the property")]
    public void A_non_text_label_is_refused()
    {
        var refusal = Refusal("ORMLITE011", Source("Doc", ", Label = nameof(Rank)"));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Rank'", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a refused Label is not registered")]
    public void A_refused_label_is_not_registered()
    {
        var (_, _, output) = Run(Source("Doc", ", Label = nameof(Rank)"));

        Assert.Null(GeneratedModel.Of(output).Soft("Doc")!.LabelColumn);
    }
}
