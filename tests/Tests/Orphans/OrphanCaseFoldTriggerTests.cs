using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

// SEC-113-02 on real SQLite, next to the reviewer's case-varied FK attack (OrphanAttackReviewTests.F1): SQLite
// attaches a trigger written ON "OREP_CHILDREN" to orep_children, so the guard has to see it and refuse before the
// first user write, instead of letting the trigger's unverified effect commit with the replacement.
[Collection("Graph")]
public sealed class OrphanCaseFoldTriggerTests
{
    [Theory]
    [InlineData(OrphanMode.Delete, "DELETE")]
    [InlineData(OrphanMode.Nullify, "UPDATE")]
    public async Task A_trigger_on_a_case_varied_table_name_refuses_the_replacement(OrphanMode mode, string evt)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync("sqlite");
        await db.SeedAsync("orep_children", 11);
        await db.ExecuteAsync("CREATE TABLE attack_log(n INTEGER NOT NULL)");
        await db.ExecuteAsync("CREATE TRIGGER attack_case AFTER " + evt
            + " ON \"OREP_CHILDREN\" BEGIN INSERT INTO attack_log VALUES(1); END");

        OrphanException refused;
        await using (var scope = await OrphanCollections.BeginAsync(db.Context, Children, Key(1), Options(mode)))
        {
            refused = await Assert.ThrowsAsync<OrphanException>(() => scope.ApplyAsync(Keep()));
        }

        Assert.Equal(OrphanError.UnverifiedEffects, refused.Error);
        Assert.Equal(OrphanOutcome.RolledBack, refused.Outcome);
        Assert.Equal(0L, await db.CountAsync("SELECT COUNT(*) FROM attack_log"));
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_children WHERE id=@p0 AND parent_id=@p1", Key(11), Key(1)));
    }
}
