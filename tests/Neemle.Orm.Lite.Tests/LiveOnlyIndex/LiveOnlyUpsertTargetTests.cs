using Neemle.Orm.Lite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.LiveOnlyIndex;

public sealed class LiveOnlyUpsertTargetTests
{
    private static string Entity(string indexes) =>
        $"[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] {indexes} public class Doc "
        + "{ [Key] public Guid Id { get; set; } public string Scope { get; set; } = \"\"; public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; } }\n";

    private static SoftDeleteMeta MetaOf(string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output).Soft("Doc")!;
    }

    [Fact(DisplayName = "AC8 the registration carries the column set of every LiveOnly index, taken from the index the CREATE statement is built from")]
    public void The_registration_carries_the_live_only_column_sets()
    {
        var meta = MetaOf(Entity(
            "[Index(\"Scope\", \"Name\", Unique = true, LiveOnly = true)] [Index(\"Name\", Unique = true, LiveOnly = true)] [Index(\"Scope\", Unique = true)]"));

        Assert.Equal(new[] { new[] { "scope", "name" }, new[] { "name" } }, meta.LiveOnlyIndexes);
        Assert.True(meta.Platform);
        Assert.Equal("id", meta.KeyColumn);
        Assert.Equal("doc", meta.EntityToken);
        Assert.Equal(new[] { "Id>id", "Scope>scope", "Name>name", "DeletedAt>deleted_at" }, meta.PropertyColumns!.Select(p => p[0] + ">" + p[1]));
    }

    [Fact(DisplayName = "AC8 a soft entity without a LiveOnly index registers no LiveOnly column set")]
    public void A_soft_entity_without_live_only_registers_no_set()
    {
        var meta = MetaOf(Entity("[Index(\"Scope\", \"Name\", Unique = true)]"));

        Assert.Null(meta.LiveOnlyIndexes);
        Assert.True(meta.Platform);
        Assert.Equal("id", meta.KeyColumn);
    }

    [Theory(DisplayName = "AC8 a conflict target matches a LiveOnly index exactly as a column set, in any order")]
    [InlineData(new[] { "scope", "name" }, true)]
    [InlineData(new[] { "name", "scope" }, true)]
    [InlineData(new[] { "scope" }, false)]
    [InlineData(new[] { "scope", "name", "code" }, false)]
    [InlineData(new[] { "scope", "code" }, false)]
    [InlineData(new string[0], false)]
    public void A_target_matches_only_the_whole_column_set(string[] conflictColumns, bool expected)
    {
        var meta = new SoftDeleteMeta("DeletedAt", new[] { new[] { "scope", "name" } });

        Assert.Equal(expected, meta.IsLiveOnlyTarget(conflictColumns));
    }

    [Fact(DisplayName = "AC8 a soft-delete registration with no LiveOnly index matches no target")]
    public void No_live_only_index_matches_nothing() =>
        Assert.False(new SoftDeleteMeta("DeletedAt").IsLiveOnlyTarget(["scope", "name"]));

    [Fact(DisplayName = "AC8 PgDialect renders the target predicate between the conflict columns and the action, and only when given one")]
    public void The_dialect_renders_the_predicate_in_the_conflict_target()
    {
        var pg = new PgDialect();

        Assert.Equal(" ON CONFLICT (\"scope\", \"name\") WHERE \"deleted_at\" IS NULL DO NOTHING",
            pg.ConflictClause(["scope", "name"], OnConflict.DoNothing, [], "docs", "\"deleted_at\" IS NULL"));
        Assert.Equal(" ON CONFLICT (\"scope\", \"name\") DO NOTHING",
            pg.ConflictClause(["scope", "name"], OnConflict.DoNothing, [], "docs"));
    }
}
