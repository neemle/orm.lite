using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Registry;

public sealed class PinnedStatusEmissionTests
{
    private const string Members =
        "[Key] public Guid Id { get; set; } public string Key { get; set; } = \"\"; public string Type { get; set; } = \"\"; public string Status { get; set; } = \"\";"
        + " public string Secret { get; set; } = \"\"; public Guid ProjectId { get; set; } public DateTime? DeletedAt { get; set; }";

    private static string Source(string arguments, string members = Members) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform{arguments})] public class Doc {{ {members} }}\n";

    private static string RegisterLine(string source)
    {
        var (diagnostics, generated, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var start = generated.IndexOf("OrmMeta.Register<global::Fixture.Doc>(", StringComparison.Ordinal);
        Assert.True(start >= 0, "no OrmMeta.Register line for Doc");
        return generated.Substring(start, generated.IndexOf('\n', start) - start).TrimEnd('\r');
    }

    private static Diagnostic Refusal(string id, string source)
    {
        var (diagnostics, _, _) = Run(source);
        return Assert.Single(diagnostics.Where(d => d.Id == id));
    }

    [Fact(DisplayName = "Pinned and StatusColumns are registered as exactly those database columns, in declaration order, after the Owners")]
    public void Pinned_and_status_columns_are_emitted_as_database_columns()
    {
        var line = RegisterLine(Source(", Owners = new[] { nameof(ProjectId) }, Pinned = new[] { nameof(Key), nameof(ProjectId) }, StatusColumns = new[] { nameof(Type), nameof(Status) }"));

        Assert.Contains(
            ", Owners: new string[] { \"project_id\" }, PinnedColumns: new string[] { \"key\", \"project_id\" }, StatusColumns: new string[] { \"type\", \"status\" }, EntityToken: \"doc\"",
            line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a property named Key is pinned as its own column and is not mistaken for the [Key] column")]
    public void A_property_named_key_is_its_own_column()
    {
        var line = RegisterLine(Source(", Pinned = new[] { nameof(Key) }"));

        Assert.Contains("PinnedColumns: new string[] { \"key\" }", line, StringComparison.Ordinal);
        Assert.Contains("KeyColumn: \"id\"", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a Pinned and a StatusColumns name with an explicit [Column] name are registered under the database column name")]
    public void Explicit_column_names_are_emitted()
    {
        var line = RegisterLine(Source(", Pinned = new[] { nameof(Secret) }, StatusColumns = new[] { nameof(Status) }",
            "[Key] public Guid Id { get; set; } [Column(\"hidden\")] public string Secret { get; set; } = \"\"; [Column(\"state\")] public string Status { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }"));

        Assert.Contains("PinnedColumns: new string[] { \"hidden\" }, StatusColumns: new string[] { \"state\" }", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a name declared twice in Pinned is registered once")]
    public void A_repeated_pinned_name_is_emitted_once()
    {
        var line = RegisterLine(Source(", Pinned = new[] { nameof(Secret), nameof(Secret) }"));

        Assert.Contains("PinnedColumns: new string[] { \"secret\" }, EntityToken", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a soft entity declaring neither Pinned nor StatusColumns registers neither argument")]
    public void Undeclared_pinned_and_status_columns_emit_no_argument()
    {
        var line = RegisterLine(Source(""));

        Assert.DoesNotContain("PinnedColumns", line, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusColumns", line, StringComparison.Ordinal);
        Assert.Contains("EntityToken: \"doc\"", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a non-soft entity registers no pinned or status columns")]
    public void A_plain_entity_registers_no_pinned_or_status_columns()
    {
        var line = RegisterLine("[Table(\"docs\")] public class Doc { " + Members + " }\n");

        Assert.DoesNotContain("PinnedColumns", line, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusColumns", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a Pinned name that is an Unversioned column compiles and is registered")]
    public void Pinned_naming_an_unversioned_column_is_accepted()
    {
        var line = RegisterLine(Source(", Unversioned = new[] { nameof(Secret) }, Pinned = new[] { nameof(Secret) }"));

        Assert.Contains("Unversioned: new string[] { \"secret\" }", line, StringComparison.Ordinal);
        Assert.Contains("PinnedColumns: new string[] { \"secret\" }", line, StringComparison.Ordinal);
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
