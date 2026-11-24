using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.SoftDelete.Registry;

public sealed class PinnedStatusEmissionTests
{
    private const string Members =
        "[Key] public Guid Id { get; set; } public string Key { get; set; } = \"\"; public string Type { get; set; } = \"\"; public string Status { get; set; } = \"\";"
        + " public string Secret { get; set; } = \"\"; public Guid ProjectId { get; set; } public DateTime? DeletedAt { get; set; }";

    private static string Source(string arguments, string members = Members) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform{arguments})] public class Doc {{ {members} }}\n";

    private static SoftDeleteMeta Meta(string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return GeneratedModel.Of(output).Soft("Doc")!;
    }

    private static Diagnostic Refusal(string id, string source)
    {
        var (diagnostics, _, _) = Run(source);
        return Assert.Single(diagnostics.Where(d => d.Id == id));
    }

    [Fact(DisplayName = "Pinned and StatusColumns are registered as exactly those database columns, in declaration order, after the Owners")]
    public void Pinned_and_status_columns_are_emitted_as_database_columns()
    {
        var meta = Meta(Source(", Owners = new[] { nameof(ProjectId) }, Pinned = new[] { nameof(Key), nameof(ProjectId) }, StatusColumns = new[] { nameof(Type), nameof(Status) }"));

        Assert.Equal(new[] { "project_id" }, meta.Owners);
        Assert.Equal(new[] { "key", "project_id" }, meta.PinnedColumns);
        Assert.Equal(new[] { "type", "status" }, meta.StatusColumns);
        Assert.Equal("doc", meta.EntityToken);
    }

    [Fact(DisplayName = "a property named Key is pinned as its own column and is not mistaken for the [Key] column")]
    public void A_property_named_key_is_its_own_column()
    {
        var meta = Meta(Source(", Pinned = new[] { nameof(Key) }"));

        Assert.Equal(new[] { "key" }, meta.PinnedColumns);
        Assert.Equal("id", meta.KeyColumn);
    }

    [Fact(DisplayName = "a Pinned and a StatusColumns name with an explicit [Column] name are registered under the database column name")]
    public void Explicit_column_names_are_emitted()
    {
        var meta = Meta(Source(", Pinned = new[] { nameof(Secret) }, StatusColumns = new[] { nameof(Status) }",
            "[Key] public Guid Id { get; set; } [Column(\"hidden\")] public string Secret { get; set; } = \"\"; [Column(\"state\")] public string Status { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }"));

        Assert.Equal(new[] { "hidden" }, meta.PinnedColumns);
        Assert.Equal(new[] { "state" }, meta.StatusColumns);
    }

    [Fact(DisplayName = "a name declared twice in Pinned is registered once")]
    public void A_repeated_pinned_name_is_emitted_once()
    {
        var meta = Meta(Source(", Pinned = new[] { nameof(Secret), nameof(Secret) }"));

        Assert.Equal(new[] { "secret" }, meta.PinnedColumns);
    }

    [Fact(DisplayName = "a soft entity declaring neither Pinned nor StatusColumns registers neither argument")]
    public void Undeclared_pinned_and_status_columns_emit_no_argument()
    {
        var meta = Meta(Source(""));

        Assert.Null(meta.PinnedColumns);
        Assert.Null(meta.StatusColumns);
        Assert.Equal("doc", meta.EntityToken);
    }

    [Fact(DisplayName = "a non-soft entity registers no pinned or status columns")]
    public void A_plain_entity_registers_no_pinned_or_status_columns()
    {
        var (diagnostics, _, output) = Run("[Table(\"docs\")] public class Doc { " + Members + " }\n");
        Assert.Empty(diagnostics);

        Assert.Null(GeneratedModel.Of(output).Soft("Doc"));
    }

    [Fact(DisplayName = "a Pinned name that is an Unversioned column compiles and is registered")]
    public void Pinned_naming_an_unversioned_column_is_accepted()
    {
        var meta = Meta(Source(", Unversioned = new[] { nameof(Secret) }, Pinned = new[] { nameof(Secret) }"));

        Assert.Equal(new[] { "secret" }, meta.Unversioned);
        Assert.Equal(new[] { "secret" }, meta.PinnedColumns);
    }

    [Fact(DisplayName = "ORMLITE012 refuses a Pinned name that is not a mapped property and names the entity and the name")]
    public void An_unmapped_pinned_name_is_refused()
    {
        var refusal = Refusal("ORMLITE012", Source(", Pinned = new[] { nameof(Key), \"Orphan\" }"));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Orphan'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Pinned", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE012 refuses a StatusColumns name that is not a mapped property and names the entity and the name")]
    public void An_unmapped_status_column_is_refused()
    {
        var refusal = Refusal("ORMLITE012", Source(", StatusColumns = new[] { \"Phase\" }"));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Phase'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("StatusColumns", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE013 refuses a status column that is also pinned and names the entity and the property")]
    public void A_status_column_that_is_also_pinned_is_refused()
    {
        var refusal = Refusal("ORMLITE013", Source(", Pinned = new[] { nameof(Status), nameof(Key) }, StatusColumns = new[] { nameof(Type), nameof(Status) }"));

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Status'", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "disjoint Pinned and StatusColumns raise no ORMLITE013")]
    public void Disjoint_pinned_and_status_columns_are_accepted()
    {
        var (diagnostics, _, _) = Run(Source(", Pinned = new[] { nameof(Key) }, StatusColumns = new[] { nameof(Status) }"));

        Assert.DoesNotContain(diagnostics, d => d.Id is "ORMLITE012" or "ORMLITE013");
    }
}
