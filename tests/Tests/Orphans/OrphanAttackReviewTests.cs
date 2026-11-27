// Provenance (§3.3): ORML-113, FULL, round 1 (§803) - reviewer-authored (codex), screened by QA (triage e45e083a).
// Committed for F1 (SEC-113-02) and F2 (SEC-113-01) with assertions unchanged; the only edits are this header and the
// mechanical removal of the F3/F4/F5 cases (held for a TL ruling, not committed) and of the usings only they needed.
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanAttackReviewTests
{
    [Fact]
    public async Task F1_Case_varied_fk_must_not_allow_cascade()
    {
        await using var db = await OrphanAttackDatabase.OpenAsync("sqlite");
        await db.SeedAsync("orep_children", 11);
        await db.ExecuteAsync("""
            CREATE TABLE attack_dependent(
                id INTEGER PRIMARY KEY,
                child_id TEXT NOT NULL
                    REFERENCES "OREP_CHILDREN"(id) ON DELETE CASCADE
            )
            """);
        await db.ExecuteAsync(
            "INSERT INTO attack_dependent(id,child_id) VALUES(1,@p0)", Key(11));
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM attack_dependent"));

        Exception? failure;
        await using (var scope = await OrphanCollections.BeginAsync(
            db.Context, Children, Key(1), Options(OrphanMode.Delete)))
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                await scope.ApplyAsync(Keep());
            });
        }

        // Current prediction: both rows disappeared and Apply committed.
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM attack_dependent"));
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_children WHERE id=@p0", Key(11)));

        var refused = Assert.IsType<OrphanException>(failure);
        Assert.Equal(OrphanError.UnverifiedEffects, refused.Error);
        Assert.Equal(OrphanOutcome.RolledBack, refused.Outcome);
    }

    [SkippableTheory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task F2_Orphan_diagnostics_must_not_publish_sql_or_values(
        string provider)
    {
        await using var db =
            await OrphanAttackDatabase.OpenAsync(provider, logParameters: true);
        await db.SeedAsync("orep_children", 11);
        db.Statements.Clear();

        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Neemle.OrmLite",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        const string marker = "violet orchard payload";
        var supplied = new ReplaceChild
        {
            Id = Key(11),
            ParentId = Key(1),
            Name = marker,
        };

        await using (var scope = await OrphanCollections.BeginAsync(
            db.Context, Children, Key(1), Options(OrphanMode.Nullify)))
        {
            var result = await scope.ApplyAsync(new(
                [Key(11)], OrphanPresentWrites<ReplaceChild>.None.Update(supplied)));
            Assert.Equal(OrphanOutcome.Committed, result.Outcome);
            Assert.Equal(1, result.Updated);
        }

        var events = db.Statements.ToArray();
        var valuesLeaked = events.Any(e => e.Parameters is { } parameters
            && parameters.Any(p =>
                p.Value == marker
                || string.Equals(p.Value, Key(1).ToString(),
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Value, Key(11).ToString(),
                    StringComparison.OrdinalIgnoreCase)));
        var sqlLeaked = events.Any(e => e.Text.Contains(
            "orep_children", StringComparison.OrdinalIgnoreCase));
        var activitySqlLeaked = activities.Any(a =>
            a.GetTagItem("db.statement") is string sql
            && sql.Contains("orep_children", StringComparison.OrdinalIgnoreCase));

        // Report only channel flags, never the captured SQL or values.
        Assert.True(!valuesLeaked && !sqlLeaked && !activitySqlLeaked,
            $"Disclosure: values={valuesLeaked}, sinkSql={sqlLeaked}, " +
            $"activitySql={activitySqlLeaked}.");
    }

    [SkippableTheory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task F2_Orphan_failure_events_must_not_publish_provider_exceptions(
        string provider)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync(provider);
        await db.SeedAsync("orep_children", 11);
        await db.ExecuteAsync(
            "INSERT INTO orep_pins(id,child_id) VALUES(@p0,@p1)", Key(91), Key(11));
        db.Statements.Clear();

        await using (var scope = await OrphanCollections.BeginAsync(
            db.Context, Children, Key(1), Options(OrphanMode.Delete)))
        {
            var failure = await Assert.ThrowsAsync<OrphanException>(
                () => scope.ApplyAsync(Keep()));
            Assert.Equal(OrphanOutcome.RolledBack, failure.Outcome);
        }

        Assert.False(db.Statements.Any(e => e.Error is DbException),
            "An orphan diagnostic event published the original provider exception.");
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_children WHERE id=@p0", Key(11)));
    }
}
