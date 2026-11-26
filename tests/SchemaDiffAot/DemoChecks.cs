using Neemle.OrmLite;

namespace Neemle.OrmLite.SchemaDiffAot;

internal static class DemoChecks
{
    internal static void Equal<T>(T expected, T actual, string property)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(property + ": expected " + expected + ", observed " + actual);
    }

    internal static async Task NoOpAsync(OrmContext context, SchemaDiffTarget target)
    {
        var observation = await global::Neemle.OrmLite.SchemaDiff.ObserveAsync(context, target);
        var report = global::Neemle.OrmLite.SchemaDiff.Compare(target, observation);
        Equal(SchemaDiffStatus.NoOp, report.Status, "public post-apply rediff");
        Equal(0, report.Changes.Count, "post-apply changes");
        try
        {
            global::Neemle.OrmLite.SchemaDiff.Emit(report, new("0050_parity", "Parity", "NoOp"));
        }
        catch (SchemaDiffException error) when (error.Error == SchemaDiffError.UnsupportedDifference) { return; }
        throw new InvalidOperationException("NoOp unexpectedly emitted a migration");
    }

    internal static async Task OriginalAsync(OrmContext context) => Equal("original-value",
        await DemoDatabase.ScalarAsync(context, "SELECT original FROM items WHERE id = 'old'"),
        "unaffected original row");

    internal static void Committed(SchemaDiffApplyResult result)
    {
        Equal(SchemaDiffApplyOutcome.Applied, result.Outcome, "explicit apply outcome");
        Equal(1, result.Units.Count, "one artifact transaction");
        Equal(SchemaUnitOutcome.Committed, result.Units[0].Outcome, "acknowledged transaction");
    }
}
