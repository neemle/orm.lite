using Neemle.OrmLite;
using Diff = global::Neemle.OrmLite.SchemaDiff;

namespace Neemle.OrmLite.SchemaDiffAot;

internal static class DemoScenario
{
    internal static async Task RunAsync(OrmContext context, string provider)
    {
        await context.MigrateAsync(DemoModel.Before().Contributions());
        await DemoDatabase.ExecuteAsync(context, "INSERT INTO items(id, original) VALUES ('old', 'original-value')");
        var target = DemoModel.Target(provider);
        var history = await DemoDatabase.HistoryAsync(context);
        var observation = await Diff.ObserveAsync(context, target);
        var report = Diff.Compare(target, observation);
        DemoChecks.Equal(SchemaDiffStatus.Changes, report.Status, "complete supported changes");
        DemoChecks.Equal(1, report.Changes.Count(c => c.Kind == SchemaDiffChangeKind.CreateTable), "new table");
        DemoChecks.Equal(1, report.Changes.Count(c => c.Kind == SchemaDiffChangeKind.AddNullableColumn), "new column");
        var artifact = Diff.Emit(report, new("0050_parity", "Parity.Generated", "CombinedRecipe"));
        DemoChecks.Equal(history, await DemoDatabase.HistoryAsync(context), "comparison/generation leaves history");
        await DemoChecks.OriginalAsync(context);
        var request = DemoModel.Request(artifact);
        DemoChecks.Committed(await SchemaDiffFixtureRunner.RunAsync(context, request));
        await DemoChecks.NoOpAsync(context, target);
        DemoChecks.Equal("NULL", await DemoDatabase.ScalarAsync(context,
            "SELECT added FROM items WHERE id = 'old'"), "new column has no backfill");
        await RepeatAsync(context, request);
        await DownUpAsync(context, target, request, history);
        Console.WriteLine("PARITY version=" + artifact.Version);
        Console.WriteLine("PARITY schema=items(id,original,added);new_items(id,value)");
        Console.WriteLine("PARITY data=old:original-value;added:NULL;new_rows:0");
        Console.WriteLine("PARITY history=" + await DemoDatabase.HistoryAsync(context));
        Console.WriteLine("PARITY outcomes=Changes,Applied,NoOp,AlreadyApplied,Reverted,Applied,NoOp");
    }

    private static async Task RepeatAsync(OrmContext context, SchemaDiffFixtureRequest request)
    {
        var history = await DemoDatabase.HistoryAsync(context);
        var repeat = await SchemaDiffFixtureRunner.RunAsync(context, request);
        DemoChecks.Equal(SchemaDiffApplyOutcome.AlreadyApplied, repeat.Outcome, "repeat outcome");
        DemoChecks.Equal(0, repeat.Units.Count, "repeat has no new transaction");
        DemoChecks.Equal(history, await DemoDatabase.HistoryAsync(context), "repeat leaves history");
    }

    private static async Task DownUpAsync(OrmContext context, SchemaDiffTarget target,
        SchemaDiffFixtureRequest request, string originalHistory)
    {
        await DemoDatabase.ExecuteAsync(context, "UPDATE items SET added = 'new-column-value' WHERE id = 'old'");
        await DemoDatabase.ExecuteAsync(context, "INSERT INTO new_items(id, value) VALUES ('new', 'new-table-value')");
        DemoChecks.Committed(await SchemaDiffFixtureRunner.RunAsync(context,
            request with { Mode = SchemaDiffFixtureMode.RevertAppliedUpgrade }));
        DemoChecks.Equal(originalHistory, await DemoDatabase.HistoryAsync(context), "Down forgets only actual version");
        await DemoChecks.OriginalAsync(context);
        var reverted = Diff.Compare(target, await Diff.ObserveAsync(context, target));
        DemoChecks.Equal(SchemaDiffStatus.Changes, reverted.Status, "Down restores before schema");
        DemoChecks.Equal(2, reverted.Changes.Count, "Down removes exactly added table and field");
        DemoChecks.Committed(await SchemaDiffFixtureRunner.RunAsync(context, request));
        await DemoChecks.NoOpAsync(context, target);
        await DemoChecks.OriginalAsync(context);
        DemoChecks.Equal("NULL", await DemoDatabase.ScalarAsync(context,
            "SELECT added FROM items WHERE id = 'old'"), "Down loses new column data");
        DemoChecks.Equal("0", await DemoDatabase.ScalarAsync(context,
            "SELECT count(*) FROM new_items"), "Down loses new table data");
    }
}
