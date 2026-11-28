using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class OrphanDemoChecks
{
    internal static OrphanLimits Limits => new(20, 20, 20, 20, 400);
    internal static OrphanOptions Options(OrphanMode mode) => new(Limits, mode);
    internal static OwnedCollection<OrphanDemoParent, OrphanDemoChild, Guid, Guid> Children =>
        OrphanCollections.Relation<OrphanDemoParent, OrphanDemoChild, Guid, Guid>(nameof(OrphanDemoParent.Children));
    internal static OwnedCollection<OrphanDemoParent, OrphanDemoSoftChild, Guid, Guid> SoftChildren =>
        OrphanCollections.Relation<OrphanDemoParent, OrphanDemoSoftChild, Guid, Guid>(
            nameof(OrphanDemoParent.SoftChildren));
    internal static void Require(bool condition, string fact)
    {
        if (!condition) throw new InvalidOperationException("Orphans assertion: " + fact);
    }
    internal static async Task RefuseAsync(Func<Task> action, OrphanError error, OrphanOutcome outcome)
    {
        try { await action(); }
        catch (OrphanException actual) when (actual.Error == error && actual.Outcome == outcome) { return; }
        throw new InvalidOperationException("Expected orphan refusal: " + error + " / " + outcome);
    }
    internal static void Counts(OrphanResult result, int[] expected)
    {
        Require(result.Outcome == OrphanOutcome.Committed && !result.Disabled, "acknowledged commit");
        Require(new[] { result.Nullified, result.Deleted, result.SoftDeleted, result.Inserted, result.Updated }
            .SequenceEqual(expected), "exact committed counts");
    }
}
