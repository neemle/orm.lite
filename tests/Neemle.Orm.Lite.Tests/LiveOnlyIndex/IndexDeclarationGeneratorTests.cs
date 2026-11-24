using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.LiveOnlyIndex;

public sealed class IndexDeclarationGeneratorTests
{
    private static string Entity(string classIndexes, string propertyIndex = "") =>
        $"[Table(\"items\")] {classIndexes} public class Item "
        + "{ [Key] public Guid Id { get; set; } public string Status { get; set; } = \"\"; public int? Priority { get; set; } "
        + $"public string Title {{ get; set; }} = \"\"; {propertyIndex} public int Count {{ get; set; }} public DateTime? DeletedAt {{ get; set; }} }}\n";

    private static IReadOnlyList<string> Indexes(Compilation output) => GeneratedModel.Render(GeneratedModel.Of(output).Schema.Indexes());

    private static IReadOnlyList<Diagnostic> Errors(IEnumerable<Diagnostic> all, string id) => all.Where(d => d.Id == id).ToList();

    [Fact(DisplayName = "a class-level [Index] lists several columns, each with its own direction, null placement and collation, and the emitted index states them column by column")]
    public void A_class_level_index_carries_a_sorted_term_per_column()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Status\", \"Priority desc nulls last\", \"Title collate NOCASE\", Name = \"ix_items_mixed\")]"));

        Assert.Empty(diagnostics);
        Assert.Equal(
            ["CREATE INDEX IF NOT EXISTS \"ix_items_mixed\" ON \"items\" (\"status\", \"priority\" DESC NULLS LAST, \"title\" COLLATE \"NOCASE\" ASC)"],
            Indexes(output));
    }

    [Fact(DisplayName = "nameof and a constant suffix spell a term, so the column stays compile-checked")]
    public void A_term_can_be_spelled_with_nameof()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(nameof(Item.Status), nameof(Item.Priority) + \" DESC\")]"));

        Assert.Empty(diagnostics);
        Assert.Equal(["CREATE INDEX IF NOT EXISTS \"ix_items_status_priority\" ON \"items\" (\"status\", \"priority\" DESC)"], Indexes(output));
    }

    [Fact(DisplayName = "an [Index] on a property indexes that one field and carries direction, null placement, collation, uniqueness and a name")]
    public void A_property_level_index_carries_every_option()
    {
        var (diagnostics, _, output) = Run(Entity("", "[Index(Descending = true, Nulls = Neemle.Orm.Lite.Nodes.NullsPlacement.First, Unique = true, Name = \"uq_items_rank\")] public int Rank { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Equal(["CREATE UNIQUE INDEX IF NOT EXISTS \"uq_items_rank\" ON \"items\" (\"rank\" DESC NULLS FIRST)"], Indexes(output));
    }

    [Fact(DisplayName = "a property-level collation and a lowered term are emitted on the property's column")]
    public void A_property_level_collation_and_lower()
    {
        var (diagnostics, _, output) = Run(Entity("", "[Index(Collation = \"NOCASE\")] public string Code { get; set; } = \"\"; [Index(Lower = true, Unique = true)] public string Handle { get; set; } = \"\";"));

        Assert.Empty(diagnostics);
        Assert.Equal(
            [
                "CREATE INDEX IF NOT EXISTS \"ix_items_code\" ON \"items\" (\"code\" COLLATE \"NOCASE\" ASC)",
                "CREATE UNIQUE INDEX IF NOT EXISTS \"ix_items_handle\" ON \"items\" (lower(\"handle\"))",
            ],
            Indexes(output));
    }

    [Fact(DisplayName = "a Where predicate is parsed against the entity's properties and emitted as the partial index's predicate")]
    public void A_where_predicate_is_a_partial_index()
    {
        var (diagnostics, _, output) = Run(Entity("[Index(\"Title\", Unique = true, Where = \"Status = 'it''s open' and Count > 3 and DeletedAt is null\")]"));

        Assert.Empty(diagnostics);
        var index = Assert.Single(Indexes(output));
        Assert.StartsWith("CREATE UNIQUE INDEX IF NOT EXISTS \"ix_items_title\" ON \"items\" (\"title\") WHERE ", index, StringComparison.Ordinal);
        Assert.Contains("\"status\" = 'it''s open'", index, StringComparison.Ordinal);
        Assert.Contains("\"count\" > 3", index, StringComparison.Ordinal);
        Assert.Contains("\"deleted_at\" IS NULL", index, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "ORMLITE020 refuses a term or condition that cannot be read, names an unknown column, repeats a column or collates a non-text one, naming the entity and the reason")]
    [InlineData("[Index(\"Nope\")]", "'Nope' is not a property or column")]
    [InlineData("[Index(\"Status\", \"Status desc\")]", "appears more than once")]
    [InlineData("[Index(\"Count collate NOCASE\")]", "collate and lower apply to text columns")]
    [InlineData("[Index(\"Status sideways\")]", "cannot read 'sideways'")]
    [InlineData("[Index(\"Status\", Descending = true)]", "belong to an [Index] on a property")]
    [InlineData("[Index(\"Status\", Where = \"Status = 4\")]", "cannot be compared with 4")]
    [InlineData("[Index(\"Status\", Where = \"Status like 'a'\")]", "cannot read the condition")]
    [InlineData("[Index(\"Status\", Where = \"Missing is null\")]", "'Missing' is not a property or column")]
    [InlineData("[Index]", "it names no column")]
    public void An_unreadable_declaration_is_a_build_error(string attribute, string reason)
    {
        var (diagnostics, _, _) = Run(Entity(attribute));

        var refusal = Assert.Single(Errors(diagnostics, "ORMLITE020"));
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Item'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(reason, refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE020 refuses a collation or lower() on a decimal column, in a class-level term and on a property, and accepts its direction")]
    public void A_collation_on_a_decimal_column_is_a_build_error()
    {
        const string amount = "[Numeric(19, 4)] public decimal Amount { get; set; }";
        var (classLevel, _, _) = Run(Entity("[Index(\"Amount collate NOCASE\")]", amount));
        var (propertyLevel, _, _) = Run(Entity("", "[Index(Collation = \"NOCASE\")] " + amount));
        var (lowered, _, _) = Run(Entity("", "[Index(Lower = true)] " + amount));
        var (direction, _, output) = Run(Entity("", "[Index(Descending = true, Unique = true)] " + amount));

        foreach (var diagnostics in new[] { classLevel, propertyLevel, lowered })
        {
            var refusal = Assert.Single(Errors(diagnostics, "ORMLITE020"));
            Assert.Contains("'Amount' is decimal", refusal.GetMessage(), StringComparison.Ordinal);
        }
        Assert.Empty(direction);
        Assert.Equal(["CREATE UNIQUE INDEX IF NOT EXISTS \"ix_items_amount\" ON \"items\" (\"amount\" DESC)"], Indexes(output));
    }

    [Fact(DisplayName = "ORMLITE020 refuses terms on a property-level [Index]")]
    public void A_property_level_index_takes_no_terms()
    {
        var (diagnostics, _, _) = Run(Entity("", "[Index(\"Title\")] public string Code { get; set; } = \"\";"));

        Assert.Contains("takes no terms", Assert.Single(Errors(diagnostics, "ORMLITE020")).GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE021 refuses two indexes that resolve to one name but differ, and accepts two that are the same declaration")]
    public void Two_different_indexes_under_one_name_are_refused()
    {
        var (different, _, _) = Run(Entity("[Index(\"Status\")] [Index(\"Status desc\")]"));
        var (same, _, _) = Run(Entity("[Index(\"Status\")] [Index(\"Status\")]"));

        var refusal = Assert.Single(Errors(different, "ORMLITE021"));
        Assert.Contains("ix_items_status", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Errors(same, "ORMLITE021"));
    }
}
