using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Registry;

public sealed class LabelOwnersEmissionTests
{
    private const string Members =
        "[Key] public Guid Id { get; set; } public string Title { get; set; } = \"\"; public Guid ProjectId { get; set; }"
        + " public Guid? ParentId { get; set; } public int Rank { get; set; } public DateTime? DeletedAt { get; set; }";

    private const string Properties =
        "new string[][] { new string[] { \"Id\", \"id\" }, new string[] { \"Title\", \"title\" }, new string[] { \"ProjectId\", \"project_id\" },"
        + " new string[] { \"ParentId\", \"parent_id\" }, new string[] { \"Rank\", \"rank\" }, new string[] { \"DeletedAt\", \"deleted_at\" } }";

    private static string Source(string type, string arguments, string members = Members) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform{arguments})] public class {type} {{ {members} }}\n";

    private static string RegisterLine(string type, string source)
    {
        var (diagnostics, generated, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var start = generated.IndexOf("OrmMeta.Register<global::Fixture." + type + ">(", StringComparison.Ordinal);
        Assert.True(start >= 0, "no OrmMeta.Register line for " + type);
        return generated.Substring(start, generated.IndexOf('\n', start) - start).TrimEnd('\r');
    }

    private static Diagnostic Refusal(string id, string source)
    {
        var (diagnostics, _, _) = Run(source);
        return Assert.Single(diagnostics.Where(d => d.Id == id));
    }

    [Fact(DisplayName = "a Label and Owners are registered as exactly those database columns, in declaration order, after the key column")]
    public void Label_and_owners_are_emitted_as_database_columns()
    {
        var line = RegisterLine("Doc", Source("Doc", ", Label = nameof(Title), Owners = new[] { nameof(ProjectId), nameof(ParentId) }"));

        Assert.EndsWith(
            ", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", Platform: true, KeyColumn: \"id\", LabelColumn: \"title\", Owners: new string[] { \"project_id\", \"parent_id\" }, EntityToken: \"doc\", PropertyColumns: "
            + Properties + "));",
            line);
    }

    [Fact(DisplayName = "a Label and an Owner with an explicit [Column] name are registered under the database column name")]
    public void Explicit_column_names_are_emitted()
    {
        var line = RegisterLine("Doc", Source("Doc", ", Label = nameof(Title), Owners = new[] { nameof(ProjectId) }",
            "[Key] public Guid Id { get; set; } [Column(\"heading\")] public string Title { get; set; } = \"\"; [Column(\"proj\")] public Guid ProjectId { get; set; } public DateTime? DeletedAt { get; set; }"));

        Assert.Contains("LabelColumn: \"heading\", Owners: new string[] { \"proj\" }, EntityToken", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an owner named twice is registered once")]
    public void A_repeated_owner_is_emitted_once()
    {
        var line = RegisterLine("Doc", Source("Doc", ", Owners = new[] { nameof(ProjectId), nameof(ProjectId) }"));

        Assert.Contains("Owners: new string[] { \"project_id\" }, EntityToken", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a soft entity declaring neither Label nor Owners registers neither argument")]
    public void Undeclared_label_and_owners_emit_no_argument()
    {
        var line = RegisterLine("Doc", Source("Doc", ""));

        Assert.DoesNotContain("LabelColumn", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Owners", line, StringComparison.Ordinal);
        Assert.Contains("EntityToken: \"doc\"", line, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "the entity token is the snake_case of the CLR type name")]
    [InlineData("WorkItem", "work_item")]
    [InlineData("SpecNode", "spec_node")]
    [InlineData("Project", "project")]
    [InlineData("VaultEntry", "vault_entry")]
    [InlineData("Page2Item", "page2_item")]
    public void The_entity_token_is_snake_case(string type, string token)
    {
        var line = RegisterLine(type, Source(type, ""));

        Assert.Contains("EntityToken: \"" + token + "\"", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a non-soft entity registers no entity token and no property map")]
    public void A_plain_entity_registers_no_soft_metadata()
    {
        var line = RegisterLine("Doc", "[Table(\"docs\")] public class Doc { " + Members + " }\n");

        Assert.DoesNotContain("SoftDeleteMeta", line, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityToken", line, StringComparison.Ordinal);
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
        var (_, generated, _) = Run(Source("Doc", ", Label = nameof(Rank)"));

        Assert.DoesNotContain("LabelColumn", generated, StringComparison.Ordinal);
    }
}
