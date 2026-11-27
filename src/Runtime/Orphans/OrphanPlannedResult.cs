namespace Neemle.OrmLite;

internal static class OrphanPlannedResult
{
    internal static OrphanResult Build<TKey>(OrphanMode mode, OrphanPrepared<TKey> plan) where TKey : notnull
    {
        var inserted = plan.Present.Count(w => w.Intent == OrphanPresentIntent.Insert);
        var updated = plan.Present.Count - inserted;
        var candidates = plan.Candidates.Count;
        var counts = new OrphanCounts(mode == OrphanMode.Nullify ? candidates : 0,
            mode == OrphanMode.Delete ? candidates : 0, mode == OrphanMode.SoftDelete ? candidates : 0,
            inserted, updated);
        return new OrphanResult(OrphanOutcome.Committed, false, counts);
    }
}
