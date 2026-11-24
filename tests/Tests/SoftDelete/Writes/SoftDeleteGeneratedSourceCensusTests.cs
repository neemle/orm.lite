using Neemle.OrmLite.Tests.MigrationReversibility;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.Writes;

public sealed class SoftDeleteGeneratedSourceCensusTests
{
    private const string SoftEntity =
        "[Table(\"census_softs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] public class CensusSoft { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; public int Rank { get; set; } public DateTime? DeletedAt { get; set; } }\n";

    private const string PlainEntity =
        "[Table(\"census_plains\")] public class CensusPlain { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static readonly (string Generated, GeneratedModel Model) Run = Generate();

    private static (string, GeneratedModel) Generate()
    {
        var run = MigrationGeneratorRun.Run(SoftEntity + PlainEntity);
        Assert.Empty(run.Diagnostics);
        return (run.Generated, GeneratedModel.Of(run.Output));
    }

    private static string DeleteText(string type) =>
        GeneratedModel.Text(EntityStatements.Delete(Run.Model.Descriptor(type), Guid.Empty));

    private static string UpdateText(string type)
    {
        var descriptor = Run.Model.Descriptor(type);
        return GeneratedModel.Text(EntityStatements.Update(descriptor, new object?[descriptor.Columns.Count]));
    }

    [Fact(DisplayName = "AC3-r2: the generated DeleteAsync(key) of a soft entity hands the descriptor to the runtime and carries no statement literal of its own; the statement the runtime builds is the soft one")]
    public void The_generated_soft_delete_body_carries_no_statement_literal()
    {
        Assert.Contains("OrmExec.DeleteAsync(", Run.Generated, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE", Run.Generated, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", Run.Generated, StringComparison.Ordinal);
        Assert.DoesNotContain("now()", Run.Generated, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("UPDATE \"census_softs\" SET \"deleted_at\" = now() WHERE \"id\" = @k AND \"deleted_at\" IS NULL", DeleteText("CensusSoft"));
    }

    [Fact(DisplayName = "AC3 control: the DeleteAsync(key) statement of a non-soft entity stays the physical DELETE")]
    public void The_generated_non_soft_delete_body_keeps_the_physical_delete()
    {
        Assert.Equal("DELETE FROM \"census_plains\" WHERE \"id\" = @k", DeleteText("CensusPlain"));
    }

    [Fact(DisplayName = "AC4: the whole-entity UpdateAsync statement of a soft entity leaves the soft column out of SET and adds the live predicate")]
    public void The_generated_soft_update_leaves_the_soft_column_out_of_the_set_clause()
    {
        var text = UpdateText("CensusSoft");

        Assert.Equal("UPDATE \"census_softs\" SET \"name\" = @s0, \"rank\" = @s1 WHERE \"id\" = @key AND \"deleted_at\" IS NULL", text);
        Assert.DoesNotContain("\"deleted_at\" = ", text);
    }

    [Fact(DisplayName = "AC4 control: the whole-entity UpdateAsync statement of a non-soft entity still assigns a column named like the soft column and has no live predicate")]
    public void The_generated_non_soft_update_is_unchanged()
    {
        Assert.Equal("UPDATE \"census_plains\" SET \"name\" = @s0, \"deleted_at\" = @s1 WHERE \"id\" = @key", UpdateText("CensusPlain"));
    }
}
