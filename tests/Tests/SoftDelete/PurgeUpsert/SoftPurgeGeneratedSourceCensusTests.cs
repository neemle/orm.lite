using System.Text.RegularExpressions;
using Neemle.OrmLite.Tests.MigrationReversibility;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.PurgeUpsert;

public sealed partial class SoftPurgeGeneratedSourceCensusTests
{
    private const string SoftEntity =
        "[Table(\"census_softs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] public class CensusSoft { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private const string OwnerEntity =
        "[Table(\"census_owners\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] public class CensusOwner { [Key] public Guid Id { get; set; } "
        + "public DateTime? DeletedAt { get; set; } [ManyToMany(\"census_owner_tags\", \"owner_id\", \"tag_id\")] "
        + "public System.Collections.Generic.List<CensusTag> Tags { get; set; } = new(); }\n";

    private const string TagEntity =
        "[Table(\"census_tags\")] public class CensusTag { [Key] public Guid Id { get; set; } }\n";

    private const string PlainEntity =
        "[Table(\"census_plains\")] public class CensusPlain { [Key] public Guid Id { get; set; } "
        + "public string Name { get; set; } = \"\"; }\n";

    private static readonly string[] SoftTables = ["census_softs", "census_owners"];

    private static readonly (System.Collections.Immutable.ImmutableArray<Diagnostic> Diagnostics, string Generated, Compilation Output) Run =
        MigrationGeneratorRun.Run(SoftEntity + OwnerEntity + TagEntity + PlainEntity);

    private static readonly GeneratedModel Model = GeneratedModel.Of(Run.Output);

    [GeneratedRegex(@"DELETE FROM ""([^""]+)""")]
    private static partial Regex DeleteTarget();

    private static string ClassBody(string name)
    {
        var start = Run.Generated.IndexOf("public static class " + name + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, name);
        var next = Run.Generated.IndexOf("\npublic static class ", start + 1, StringComparison.Ordinal);
        return next < 0 ? Run.Generated[start..] : Run.Generated[start..next];
    }

    private static IEnumerable<string> PhysicalDeletes()
    {
        foreach (var type in new[] { "CensusSoft", "CensusOwner", "CensusTag", "CensusPlain" })
        {
            var descriptor = Model.Descriptor(type);
            yield return GeneratedModel.Text(EntityStatements.Delete(descriptor, Guid.Empty));
            foreach (var link in descriptor.ManyToMany)
                yield return GeneratedModel.Text(EntityStatements.RemoveLink(link, Guid.Empty, Guid.Empty));
        }
    }

    [Fact(DisplayName = "AC7: the generated source of soft and non-soft entities compiles clean")]
    public void The_generated_source_compiles()
    {
        Assert.Empty(Run.Diagnostics);
        Assert.Empty(Run.Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact(DisplayName = "AC7 census: no statement the generated entities build physically deletes from a soft-delete table, so only the purge surface can")]
    public void No_generated_statement_deletes_from_a_soft_table()
    {
        var targets = PhysicalDeletes().Select(s => DeleteTarget().Match(s)).Where(m => m.Success).Select(m => m.Groups[1].Value)
            .Distinct().Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["census_owner_tags", "census_plains", "census_tags"], targets);
        Assert.Empty(targets.Intersect(SoftTables));
    }

    [Theory(DisplayName = "AC7: a soft entity gets a generated Purge class whose DeleteWhereAsync overloads call the runtime PurgeAsync and carry no statement of their own")]
    [InlineData("CensusSoft")]
    [InlineData("CensusOwner")]
    public void A_soft_entity_gets_a_purge_surface(string entity)
    {
        var body = ClassBody(entity + "Purge");

        Assert.Equal(2, Regex.Matches(body, "public static async global::System.Threading.Tasks.Task<int> DeleteWhereAsync\\(").Count);
        Assert.Equal(2, Regex.Matches(body, Regex.Escape("Orm.Query(db)")).Count);
        Assert.Equal(2, Regex.Matches(body, Regex.Escape(".Where(@where).PurgeAsync()")).Count);
        Assert.Contains("global::System.Linq.Expressions.Expression<global::System.Func<global::Fixture." + entity + ", bool>> @where", body);
        Assert.Contains("global::Neemle.OrmLite.UnitOfWork uow", body);
        Assert.DoesNotContain("DELETE", body);
        Assert.DoesNotContain("CommandText", body);
    }

    [Fact(DisplayName = "AC7 control: a non-soft entity gets no Purge class and keeps its physical DeleteAsync statement")]
    public void A_non_soft_entity_gets_no_purge_surface()
    {
        Assert.DoesNotContain("CensusPlainPurge", Run.Generated);
        Assert.DoesNotContain("CensusTagPurge", Run.Generated);
        Assert.Equal("DELETE FROM \"census_plains\" WHERE \"id\" = @k", GeneratedModel.Text(EntityStatements.Delete(Model.Descriptor("CensusPlain"), Guid.Empty)));
    }

    [Fact(DisplayName = "AC7: the Orm class of a soft entity carries no DELETE statement and no purge member, and its DeleteAsync statement is the soft one")]
    public void The_orm_class_of_a_soft_entity_exposes_no_physical_delete()
    {
        var body = ClassBody("CensusSoftOrm");

        Assert.DoesNotContain("DELETE", body);
        Assert.DoesNotContain("Purge", body);
        Assert.StartsWith("UPDATE \"census_softs\"", GeneratedModel.Text(EntityStatements.Delete(Model.Descriptor("CensusSoft"), Guid.Empty)));
    }
}
