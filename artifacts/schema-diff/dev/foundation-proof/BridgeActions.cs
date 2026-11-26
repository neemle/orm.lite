using Neemle.OrmLite;

namespace FoundationProof;

internal static class BridgeActions
{
    internal static SchemaDiffFixtureRequest Request(SchemaDiffArtifact artifact, BridgeCase shape,
        SchemaDiffFixtureMode mode) => new(artifact, mode, new(artifact.ReviewDigest, "fixture117"),
            new(shape.Target.Schema, Array.Empty<string>()));

    internal static async Task UpgradeAsync(OrmContext context, BridgeCase shape, SchemaDiffArtifact artifact)
    {
        var request = Request(artifact, shape, SchemaDiffFixtureMode.UpgradeObserved);
        await RefusalAsync(context, request with { Review = new("wrong-review", "fixture117") },
            SchemaDiffError.ReviewMismatch);
        await RefusalAsync(context, request with { Lineage = new(shape.Target.Schema, ["0100_missing_manual"]) },
            SchemaDiffError.RequiresManualMigrationFirst);
        var applied = await SchemaDiffFixtureRunner.RunAsync(context, request);
        Check.That(applied.Outcome == SchemaDiffApplyOutcome.Applied && applied.Units.Count == 1
            && applied.Units.Single().Outcome == SchemaUnitOutcome.Committed, "actual upgrade committed");
        var repeat = await SchemaDiffFixtureRunner.RunAsync(context, request);
        Check.That(repeat.Outcome == SchemaDiffApplyOutcome.AlreadyApplied && repeat.Units.Count == 0,
            "repeat guard returns zero mutation units");
        await RoundtripAsync(context, shape, artifact);
        Console.WriteLine("BRIDGE_UPGRADE_REPEAT_DOWN_UP_PASS " + context.Provider.Id + " create=" + shape.Created);
    }

    private static async Task RoundtripAsync(OrmContext context, BridgeCase shape, SchemaDiffArtifact artifact)
    {
        var table = shape.Target.Scope.OwnedTables.Single();
        if (shape.Created)
            await BridgeSql.ExecuteAsync(context.DataSource, "INSERT INTO " + BridgeSql.Quoted(table)
                + " (id,title,added) VALUES ('created-id','created-title','post-up-private')");
        else await BridgeSql.ExecuteAsync(context.DataSource, "UPDATE " + BridgeSql.Quoted(table)
            + " SET added='post-up-private'");
        var down = Request(artifact, shape, SchemaDiffFixtureMode.RevertAppliedUpgrade);
        var reverted = await SchemaDiffFixtureRunner.RunAsync(context, down);
        Check.That(reverted.Units.Single().Outcome == SchemaUnitOutcome.Committed, "actual reviewed Down committed");
        await RefusalAsync(context, down, SchemaDiffError.HistoryMismatch);
        if (!shape.Created) await OriginalAsync(context, table);
        var up = await SchemaDiffFixtureRunner.RunAsync(context, Request(artifact, shape,
            SchemaDiffFixtureMode.UpgradeObserved));
        Check.That(up.Outcome == SchemaDiffApplyOutcome.Applied, "actual second Up");
        var value = shape.Created
            ? await BridgeSql.ScalarAsync(context.DataSource, "SELECT count(*) FROM " + BridgeSql.Quoted(table))
            : await BridgeSql.ScalarAsync(context.DataSource, "SELECT added FROM " + BridgeSql.Quoted(table));
        Check.That(shape.Created ? Convert.ToInt64(value) == 0 : value is DBNull,
            "Down loses newly inserted table/field data; second Up does not restore it");
        if (!shape.Created) await OriginalAsync(context, table);
        Console.WriteLine("DOWN_POST_UP_DATA_LOSS_DEMONSTRATED " + context.Provider.Id);
    }

    private static async Task OriginalAsync(OrmContext context, string table) =>
        Check.That((string?)await BridgeSql.ScalarAsync(context.DataSource,
             "SELECT title FROM " + BridgeSql.Quoted(table)
            + " WHERE id='existing-id'") == "original-private-title", "unaffected original data preserved");

    internal static async Task FreshAsync(OrmContext context, BridgeCase shape, SchemaDiffArtifact artifact)
    {
        var request = Request(artifact, shape, SchemaDiffFixtureMode.FreshCurrentBaseline);
        var applied = await SchemaDiffFixtureRunner.RunAsync(context, request);
        Check.That(applied.Outcome == SchemaDiffApplyOutcome.CoveredByCurrentBaseline,
             "actual fresh baseline coverage");
        Check.That(applied.KnownCommittedVersions.Contains(artifact.CoverageVersion)
            && !applied.KnownCommittedVersions.Contains(artifact.Version), "coverage is not fake executed upgrade");
        var observed = await SchemaDiff.ObserveAsync(context, shape.Target);
        var versions = observed.History.AppliedVersions.GetKnown();
        Check.That(versions.Contains(artifact.CoverageVersion) && !versions.Contains(artifact.Version),
            "actual history carries only coverage marker");
        var repeat = await SchemaDiffFixtureRunner.RunAsync(context, request);
        Check.That(repeat.Outcome == SchemaDiffApplyOutcome.CoveredByCurrentBaseline && repeat.Units.Count == 0,
            "fresh repeat has zero mutation units");
        await RefusalAsync(context, Request(artifact, shape, SchemaDiffFixtureMode.RevertAppliedUpgrade),
            SchemaDiffError.BaselineOwnsObjects);
        Console.WriteLine("BRIDGE_FRESH_COVERAGE_REPEAT_PASS " + context.Provider.Id + " create=" + shape.Created);
    }

    private static async Task RefusalAsync(OrmContext context, SchemaDiffFixtureRequest request,
             SchemaDiffError category)
    {
        try { await SchemaDiffFixtureRunner.RunAsync(context, request); }
        catch (SchemaDiffException error) { Check.That(error.Error == category, "exact refusal category"); return; }
        throw new IOException("expected refusal " + category);
    }
}
