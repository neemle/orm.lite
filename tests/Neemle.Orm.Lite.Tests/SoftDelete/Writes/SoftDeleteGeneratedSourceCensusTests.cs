using Neemle.Orm.Lite.Tests.MigrationReversibility;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Writes;

public sealed class SoftDeleteGeneratedSourceCensusTests
{
    private const string DeleteByKeyMember = "public static async Task<int> DeleteAsync(DbConnection conn, ";
    private const string UpdateMember = "public static async Task UpdateAsync(DbConnection conn, ";

    private const string SoftEntity =
        "[Table(\"census_softs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] public class CensusSoft { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; public int Rank { get; set; } public DateTime? DeletedAt { get; set; } }\n";

    private const string PlainEntity =
        "[Table(\"census_plains\")] public class CensusPlain { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static readonly string Generated = Run();

    private static string Run()
    {
        var run = MigrationGeneratorRun.Run(SoftEntity + PlainEntity);
        Assert.Empty(run.Diagnostics);
        return run.Generated;
    }

    private static string BodyOf(string entity, string member)
    {
        var start = Generated.IndexOf("public static class " + entity + "Orm", StringComparison.Ordinal);
        Assert.True(start >= 0, entity);
        var at = Generated.IndexOf(member, start, StringComparison.Ordinal);
        Assert.True(at >= 0, entity + " " + member);
        var end = Generated.IndexOf("\n    }\n", at, StringComparison.Ordinal);
        return Generated[at..end];
    }

    [Fact(DisplayName = "AC3-r2: the generated DeleteAsync(key) of a soft entity delegates to the runtime renderer and carries no SET, now() or DELETE literal of its own")]
    public void The_generated_soft_delete_body_carries_no_statement_literal()
    {
        var body = BodyOf("CensusSoft", DeleteByKeyMember);

        Assert.Contains("global::Neemle.Orm.Lite.SoftDeleteSql.DeleteByKey(", body);
        Assert.DoesNotContain("SET", body);
        Assert.DoesNotContain("now()", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", body);
        Assert.DoesNotContain("UPDATE", body);
    }

    [Fact(DisplayName = "AC3 control: the generated DeleteAsync(key) of a non-soft entity keeps its physical DELETE literal and does not delegate")]
    public void The_generated_non_soft_delete_body_keeps_the_physical_delete()
    {
        var body = BodyOf("CensusPlain", DeleteByKeyMember);

        Assert.Contains("DELETE FROM \\\"census_plains\\\" WHERE \\\"id\\\" = @k", body);
        Assert.DoesNotContain("SoftDeleteSql", body);
    }

    [Fact(DisplayName = "AC4: the generated whole-entity UpdateAsync of a soft entity leaves the soft column out of SET and adds the live predicate")]
    public void The_generated_soft_update_leaves_the_soft_column_out_of_the_set_clause()
    {
        var body = BodyOf("CensusSoft", UpdateMember);

        Assert.Contains("UPDATE \\\"census_softs\\\" SET \\\"name\\\" = @s0, \\\"rank\\\" = @s1 WHERE \\\"id\\\" = @key AND \\\"deleted_at\\\" IS NULL", body);
        Assert.DoesNotContain("\\\"deleted_at\\\" = ", body);
    }

    [Fact(DisplayName = "AC4 control: the generated whole-entity UpdateAsync of a non-soft entity still assigns a column named like the soft column and has no live predicate")]
    public void The_generated_non_soft_update_is_unchanged()
    {
        var body = BodyOf("CensusPlain", UpdateMember);

        Assert.Contains("UPDATE \\\"census_plains\\\" SET \\\"name\\\" = @s0, \\\"deleted_at\\\" = @s1 WHERE \\\"id\\\" = @key\"", body);
    }
}
