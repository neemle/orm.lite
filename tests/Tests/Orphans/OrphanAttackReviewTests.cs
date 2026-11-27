// Provenance (§3.3): ORML-113, FULL, round 1 (§803) - reviewer-authored, screened by QA (triage e45e083a).
// F1 (SEC-113-02) and F2 (SEC-113-01) are committed with assertions unchanged. F3 and F5 are HELD (§803.8): each
// carries a Skip naming the item that owes its redesign (ORML-152, ORML-150) and is allow-listed in
// tests/skip-allowlist.txt; neither is weakened. The reviewer withdrew F4, so it is not committed. The only edits are
// this header, the two HELD Skip markers and the removal of F4 with the one using only it needed.
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using static Neemle.OrmLite.Tests.Orphans.ReplaceModel;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanAttackReviewTests
{
    // HELD markers (§803.8): each names the routing record and the item owing the redesign. Remove the Skip when that
    // item lands; the test then runs unchanged and must PASS as an ordinary regression guard.
    private const string F3Held = "HELD (§803.8): ORML-152 owes the redesign. An unrelated PostgreSQL writer "
        + "still makes history-backed SoftDelete refuse with UnverifiedEffects "
        + "(§803 ORML-113 FULL 3daf6e6a round 1, triage e45e083a).";

    private const string F5Held = "HELD (§803.8): ORML-150 owes the redesign. The orphan emitters parse "
        + "generated C# with Roslyn instead of building it through Composer "
        + "(§803 ORML-113 FULL 3daf6e6a round 1, triage e45e083a).";

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

    [SkippableTheory(Skip = F3Held)]
    [InlineData("postgres")]
    public async Task F3_Unrelated_writer_must_not_disable_soft_replacement(
        string provider)
    {
        await using var db = await OrphanAttackDatabase.OpenAsync(provider);

        // Match the production privilege gate, rather than mistake a
        // permissions refusal for the unrelated-writer defect.
        var permitted = Convert.ToBoolean(await db.ScalarAsync("""
            SELECT pg_catalog.bool_and(
                pg_catalog.has_table_privilege(c,'UPDATE,DELETE,TRUNCATE'))
                AND (
                    pg_catalog.current_setting('is_superuser')='on'
                    OR pg_catalog.pg_has_role(
                        current_user,'pg_read_all_stats','USAGE')
                )
            FROM pg_catalog.unnest(ARRAY[
                'pg_catalog.pg_proc',
                'pg_catalog.pg_language',
                'pg_catalog.pg_namespace',
                'pg_catalog.pg_class',
                'pg_catalog.pg_type',
                'pg_catalog.pg_operator',
                'pg_catalog.pg_cast'
            ]) c
            """));
        Skip.If(!permitted, "The configured role cannot pass the history guard.");

        await db.ExecuteAsync(
            "CREATE TABLE attack_unrelated(id integer PRIMARY KEY,n integer NOT NULL)");
        await db.ExecuteAsync("INSERT INTO attack_unrelated VALUES(1,0)");
        foreach (var id in new[] { 41, 42, 43 })
            await db.SeedAsync("orep_soft", id);

        // Positive control with the same schema, role, and generated history.
        Assert.Equal(1, (await SoftAsync(db, 42, 43)).SoftDeleted);

        Exception? duringWriter;
        await using (var connection =
            await db.Context.DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using (var command = GuardNativeDatabase.Command(
                connection, transaction,
                "UPDATE attack_unrelated SET n=n+1 WHERE id=1"))
            {
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }

            try
            {
                duringWriter = await Record.ExceptionAsync(async () =>
                {
                    var result = await SoftAsync(db, 43);
                    Assert.Equal(OrphanOutcome.Committed, result.Outcome);
                    Assert.Equal(1, result.SoftDeleted);
                });
            }
            finally
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
        }

        // Positive control after releasing the unrelated writer. If the
        // intervening operation rolled back, both remaining rows are candidates.
        var after = await SoftAsync(db);
        Assert.Equal(OrphanOutcome.Committed, after.Outcome);
        Assert.Equal(3L, await db.CountAsync(
            "SELECT COUNT(*) FROM orep_soft WHERE deleted_at IS NOT NULL"));

        var category = duringWriter is OrphanException orphan
            ? $"{orphan.Error}/{orphan.Outcome}"
            : duringWriter?.GetType().Name ?? "none";
        Assert.True(duringWriter is null,
            "An unrelated writer caused replacement failure: " + category);
    }

    private static async Task<OrphanResult> SoftAsync(
        OrphanAttackDatabase db, params int[] desired)
    {
        await using var scope = await OrphanCollections.BeginAsync(
            db.Context, SoftChildren, Key(1), Options(OrphanMode.SoftDelete));
        return await scope.ApplyAsync(new(
            desired.Select(Key).ToArray(),
            OrphanPresentWrites<ReplaceSoftChild>.None));
    }

    [Theory(Skip = F5Held)]
    [InlineData("OrphanBootstrapEmitter.cs")]
    [InlineData("OrphanSourceEmitter.cs")]
    public void F5_Orphan_emission_must_not_parse_generated_csharp(string file)
    {
        // Standard tests/Tests/bin/{configuration}/net10.0 output location.
        // Reads repository source only; emits no assembly or generated file.
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../.."));
        var path = Path.Combine(root, "src", "Generator", "Orphans", file);
        Assert.True(File.Exists(path), "Run from the standard Tests project output.");

        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
        var forbidden = syntax.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(call => call.Expression.ToString())
            .Where(name => name is
                "SyntaxFactory.ParseStatement"
                or "SyntaxFactory.ParseMemberDeclaration"
                or "CSharpSyntaxTree.ParseText")
            .ToArray();

        Assert.Empty(forbidden);
    }
}
