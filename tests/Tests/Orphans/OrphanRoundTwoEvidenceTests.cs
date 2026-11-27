// Provenance (§3.3): ORML-113, FULL, round 2 (§803 record d8c4d421) - reviewer-authored, screened by QA, which ran
// all 16 cases green. Placed verbatim at the path and namespace QA ran; the only edit is this header.
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanRoundTwoEvidenceTests
{
    // Expected PASS: alternate quoting must not hide incoming CASCADE.
    [Theory]
    [InlineData("[OREP_CHILDREN]")]
    [InlineData("`OrEp_ChIlDrEn`")]
    [InlineData("\"OREP_CHILDREN\"")]
    public async Task Quoting_does_not_hide_case_varied_incoming_fk(string target)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync("sqlite");
        await db.SeedAsync("orep_children", 11);
        await db.ExecuteAsync(
            "CREATE TABLE attack_dependent(" +
            "id INTEGER PRIMARY KEY, child_id TEXT NOT NULL REFERENCES " +
            target + "(id) ON DELETE CASCADE)");
        await db.ExecuteAsync(
            "INSERT INTO attack_dependent VALUES(1,@p0)", Key(11));

        await using (var scope = await OrphanCollections.BeginAsync(
            db.Context, Children, Key(1), Options(OrphanMode.Delete)))
        {
            var error = await Assert.ThrowsAsync<OrphanException>(
                () => scope.ApplyAsync(Keep()));
            Assert.Equal(OrphanError.UnverifiedEffects, error.Error);
            Assert.Equal(OrphanOutcome.RolledBack, error.Outcome);
        }

        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM attack_dependent"));
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_children WHERE id=@p0", Key(11)));
    }

    // Expected PASS: exercise the actual TEMP catalog on the exact connection.
    [Theory]
    [InlineData("DELETE")]
    [InlineData("UPDATE")]
    public async Task Temp_trigger_on_qualified_case_varied_child_refuses(string evt)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync("sqlite");
        await db.SeedAsync("orep_children", 11);
        await using var unit = await UnitOfWork.BeginAsync(db.Context);

        await using (var command = unit.Connection.CreateCommand())
        {
            command.Transaction = unit.Transaction;
            command.CommandText = "CREATE TEMP TABLE attack_log(n INTEGER)";
            await command.ExecuteNonQueryAsync();
            command.CommandText =
                "CREATE TEMP TRIGGER attack_temp AFTER " + evt +
                " ON main.[OREP_CHILDREN] " +
                "BEGIN INSERT INTO attack_log VALUES(1); END";
            await command.ExecuteNonQueryAsync();
        }

        var request = new OrphanGuardRequest(
            Parent, Child, "parent_id", Key(1), Options(OrphanMode.Delete).Limits);
        var guard = Assert.IsAssignableFrom<IOrphanSnapshotGuard>(
            db.Context.Provider);

        var error = await Assert.ThrowsAsync<OrphanException>(() =>
            guard.AcquireAsync(
                unit.Connection, unit.Transaction, request, CancellationToken.None));

        Assert.Equal(OrphanError.UnverifiedEffects, error.Error);
        Assert.Equal(OrphanOutcome.NotStarted, error.Outcome);
        // Unit disposal rolls back the TEMP setup and releases the writer.
    }

    // Expected PASS: present Insert/Update plus each orphan mode use redaction.
    [SkippableTheory]
    [InlineData("sqlite", OrphanMode.Nullify)]
    [InlineData("postgres", OrphanMode.Nullify)]
    [InlineData("sqlite", OrphanMode.Delete)]
    [InlineData("postgres", OrphanMode.Delete)]
    [InlineData("sqlite", OrphanMode.SoftDelete)]
    [InlineData("postgres", OrphanMode.SoftDelete)]
    public async Task Every_write_mode_redacts_sink_and_activity(
        string provider, OrphanMode mode)
    {
        await using var db =
            await OrphanAttackDatabase.OpenAsync(provider, logParameters: true);
        var table = mode == OrphanMode.SoftDelete ? "orep_soft" : "orep_children";
        await db.SeedAsync(table, 11);
        await db.SeedAsync(table, 12);
        db.Statements.Clear();

        var activities = new ConcurrentQueue<Activity>();
        using var listener = Listen(activities);
        const string marker = "round-two-private-payload";
        OrphanResult result;

        if (mode == OrphanMode.SoftDelete)
        {
            var update = new ReplaceSoftChild
            {
                Id = Key(11), ParentId = Key(1), Name = marker,
            };
            var insert = new ReplaceSoftChild
            {
                Id = Key(13), ParentId = Key(1), Name = marker,
            };
            await using var scope = await OrphanCollections.BeginAsync(
                db.Context, SoftChildren, Key(1), Options(mode));
            result = await scope.ApplyAsync(new(
                [Key(11), Key(13)],
                OrphanPresentWrites<ReplaceSoftChild>.None
                    .Update(update).Insert(insert)));
        }
        else
        {
            var update = new ReplaceChild
            {
                Id = Key(11), ParentId = Key(1), Name = marker,
            };
            var insert = new ReplaceChild
            {
                Id = Key(13), ParentId = Key(1), Name = marker,
            };
            await using var scope = await OrphanCollections.BeginAsync(
                db.Context, Children, Key(1), Options(mode));
            result = await scope.ApplyAsync(new(
                [Key(11), Key(13)],
                OrphanPresentWrites<ReplaceChild>.None
                    .Update(update).Insert(insert)));
        }

        Assert.Equal(OrphanOutcome.Committed, result.Outcome);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Nullified + result.Deleted + result.SoftDeleted);
        Assert.Equal(3, db.Statements.Count);
        Assert.Equal(3, activities.Count);
        AssertRedacted(db, activities);
    }

    // Expected PASS: actual provider failure remains safe in every ORM channel.
    [SkippableTheory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task Failed_delete_redacts_errors_and_activity(string provider)
    {
        await using var db =
            await OrphanAttackDatabase.OpenAsync(provider, logParameters: true);
        await db.SeedAsync("orep_children", 11);
        await db.ExecuteAsync(
            "INSERT INTO orep_pins(id,child_id) VALUES(@p0,@p1)",
            Key(91), Key(11));
        db.Statements.Clear();

        var activities = new ConcurrentQueue<Activity>();
        using var listener = Listen(activities);

        await using (var scope = await OrphanCollections.BeginAsync(
            db.Context, Children, Key(1), Options(OrphanMode.Delete)))
        {
            var error = await Assert.ThrowsAsync<OrphanException>(
                () => scope.ApplyAsync(Keep()));
            Assert.Equal(OrphanOutcome.RolledBack, error.Outcome);
            Assert.Empty(error.Data);
            Assert.DoesNotContain("orep_", error.Message);
            Assert.DoesNotContain("orep_", error.ToString());
            Assert.DoesNotContain(Key(11).ToString(), error.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotNull(Assert.Single(db.Statements).Error);
        Assert.Single(activities);
        AssertRedacted(db, activities);
        Assert.Equal(1L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_children WHERE id=@p0", Key(11)));
    }

    // Expected PASS: a cancellation's sensitive cause/Data is not copied out.
    [Fact]
    public void Cancellation_wrapper_keeps_cause_out_of_routine_surfaces()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        const string marker = "private-cancellation-sql-and-key";
        var cause = new OperationCanceledException(marker, cancellation.Token);
        cause.Data["statement"] = marker;

        var error = Assert.IsType<OrphanCanceledException>(
            OrphanGuardFailure.Safe(cause, cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Empty(error.Data);
        Assert.DoesNotContain(marker, error.Message);
        Assert.DoesNotContain(marker, error.ToString());
        Assert.Same(cause, error.InnerException);
    }

    // Expected PASS; closes F7 without restoring SQL in diagnostics.
    [SkippableTheory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task Prepared_update_precedes_every_delete(string provider)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync(provider);
        var operation = new OrphanOperation(
            db.Context, Children, Key(1), Options(OrphanMode.Delete));
        var update = new ReplaceChild
        {
            Id = Key(11), ParentId = Key(1), Name = "changed",
        };
        var request = new OrphanReplacementRequest<ReplaceChild, Guid>(
            [Key(11)], OrphanPresentWrites<ReplaceChild>.None.Update(update));
        var plan = OrphanPlan<ReplaceChild, Guid>.Build(
            operation, [Key(11), Key(12), Key(13)], request);
        var commands = OrphanStatements.Build(operation, plan);

        Assert.Equal(3, commands.Count);
        Assert.StartsWith("UPDATE", commands[0].Statement.Text.TrimStart(),
            StringComparison.OrdinalIgnoreCase);
        Assert.All(commands.Skip(1), command =>
            Assert.StartsWith("DELETE", command.Statement.Text.TrimStart(),
                StringComparison.OrdinalIgnoreCase));
        Assert.Empty(db.Statements);
    }

    private static ActivityListener Listen(ConcurrentQueue<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Neemle.OrmLite",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void AssertRedacted(
        OrphanAttackDatabase db, IEnumerable<Activity> activities)
    {
        Assert.NotEmpty(db.Statements);
        Assert.All(db.Statements, statement =>
        {
            Assert.Equal(StatementKind.Write, statement.Kind);
            Assert.Equal("graph-save", statement.Text);
            Assert.Null(statement.Parameters);
            Assert.False(statement.Error is DbException);
            if (statement.Error is { } error)
            {
                Assert.Null(error.InnerException);
                Assert.Empty(error.Data);
                Assert.DoesNotContain("orep_", error.ToString());
            }
        });

        Assert.All(activities, activity =>
        {
            Assert.Equal("orm.graph-save", activity.OperationName);
            Assert.Equal("orm.graph-save", activity.DisplayName);
            Assert.Equal("graph-save", activity.GetTagItem("db.statement"));
            Assert.Null(activity.StatusDescription);
            Assert.Empty(activity.Events);
            Assert.All(activity.TagObjects, tag =>
                Assert.True(tag.Key is "db.system" or "db.statement"));
        });
    }
}
