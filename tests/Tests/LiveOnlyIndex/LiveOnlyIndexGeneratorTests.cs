using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.LiveOnlyIndex;

public sealed class LiveOnlyIndexGeneratorTests
{
    private static string Entity(string name, string table, string indexes, bool soft = true) =>
        $"[Table(\"{table}\")] {(soft ? "[SoftDelete(Tenant = SoftDeleteAttribute.Platform)] " : "")}{indexes} public class {name} "
        + "{ [Key] public Guid Id { get; set; } public string Scope { get; set; } = \"\"; public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static string Statement(string name, string table, string unique, string where) =>
        $"CREATE {unique}INDEX IF NOT EXISTS \"{name}\" ON \"{table}\" (\"scope\", \"name\"){where}";

    private static IReadOnlyList<string> Indexes(Compilation output) => GeneratedModel.Render(GeneratedModel.Of(output).Schema.Indexes());

    [Fact(DisplayName = "AC1 a LiveOnly unique index on a soft entity is emitted as a partial unique index over the live rows under a _live name")]
    public void A_live_only_index_is_a_partial_unique_index_under_a_live_name()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs", "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)]"));

        Assert.Empty(diagnostics);
        Assert.Contains(Statement("ix_docs_scope_name_live", "docs", "UNIQUE ", " WHERE \"deleted_at\" IS NULL"), Indexes(output));
    }

    [Fact(DisplayName = "AC2 an index that is not LiveOnly is emitted exactly as before: no WHERE and no _live name")]
    public void A_plain_index_carries_no_predicate()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs",
            "[Index(\"Scope\", \"Name\", Unique = true)] [Index(\"Name\", \"Scope\")]"));

        Assert.Empty(diagnostics);
        var indexes = Indexes(output);
        Assert.Contains(Statement("ix_docs_scope_name", "docs", "UNIQUE ", ""), indexes);
        Assert.Contains("CREATE INDEX IF NOT EXISTS \"ix_docs_name_scope\" ON \"docs\" (\"name\", \"scope\")", indexes);
        Assert.DoesNotContain(indexes, s => s.Contains("_live", StringComparison.Ordinal));
        Assert.DoesNotContain(indexes, s => s.Contains(") WHERE ", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "AC1 an explicit Name on a LiveOnly index is used as given")]
    public void An_explicit_name_is_kept_on_a_live_only_index()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs", "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true, Name = \"uq_docs_live\")]"));

        Assert.Empty(diagnostics);
        Assert.Contains(Statement("uq_docs_live", "docs", "UNIQUE ", " WHERE \"deleted_at\" IS NULL"), Indexes(output));
    }

    [Fact(DisplayName = "AC3 ORMLITE004 refuses a LiveOnly index on an entity that is not [SoftDelete], naming the entity and the index")]
    public void Live_only_on_a_non_soft_entity_is_a_build_error()
    {
        var (diagnostics, _, _) = Run(
            Entity("Plain", "plains", "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)]", soft: false)
            + Entity("Fine", "fines", "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)]"));

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE004");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("'Plain'", message);
        Assert.Contains("ix_plains_scope_name_live", message);
        Assert.Contains("not [SoftDelete]", message);
        Assert.DoesNotContain("Fine", message);
    }

    [Fact(DisplayName = "AC3 ORMLITE004 refuses a LiveOnly index that is not Unique, naming the entity and the index")]
    public void Live_only_on_a_non_unique_index_is_a_build_error()
    {
        var (diagnostics, _, _) = Run(Entity("Doc", "docs", "[Index(\"Scope\", \"Name\", LiveOnly = true)]"));

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE004");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("'Doc'", message);
        Assert.Contains("ix_docs_scope_name_live", message);
        Assert.Contains("not Unique", message);
        Assert.DoesNotContain("not [SoftDelete]", message);
    }

    [Fact(DisplayName = "AC3 a LiveOnly index that is both on a non-soft entity and not Unique is one refusal naming both reasons")]
    public void Both_reasons_are_named_in_one_refusal()
    {
        var (diagnostics, _, _) = Run(Entity("Plain", "plains", "[Index(\"Scope\", \"Name\", LiveOnly = true)]", soft: false));

        var message = Assert.Single(diagnostics, d => d.Id == "ORMLITE004").GetMessage();
        Assert.Contains("not [SoftDelete]", message);
        Assert.Contains("not Unique", message);
    }

    [Fact(DisplayName = "AC3 a valid LiveOnly declaration raises neither ORMLITE004 nor ORMLITE005")]
    public void A_valid_declaration_raises_no_index_diagnostic()
    {
        var (diagnostics, _, _) = Run(Entity("Doc", "docs", "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Name\", Unique = true)]"));

        Assert.DoesNotContain(diagnostics, d => d.Id is "ORMLITE004" or "ORMLITE005");
    }

    [Fact(DisplayName = "ORMLITE005 refuses an explicit index Name equal to another index of the same entity, naming the entity and the name")]
    public void An_explicit_name_equal_to_a_sibling_is_a_build_error()
    {
        var (diagnostics, _, _) = Run(Entity("Doc", "docs",
            "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Scope\", Name = \"ix_docs_scope_name_live\")]"));

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE005");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("'Doc'", message);
        Assert.Contains("ix_docs_scope_name_live", message);
    }

    [Fact(DisplayName = "the same default name from two undeclared names is not ORMLITE005: only an explicit Name is refused")]
    public void A_repeated_default_name_is_not_an_explicit_name_collision()
    {
        var (diagnostics, _, _) = Run(Entity("Doc", "docs", "[Index(\"Scope\")] [Index(\"Scope\", Unique = true)]"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE005");
    }
}
