namespace Neemle.OrmLite.Tests.Corpus.Cases;

internal static class LocksIntervalsCases
{
    private const string G = "locks-intervals";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var ids = new List<Guid> { a, CorpusWorld.RowB };
        var array = new[] { CorpusWorld.RowB, CorpusWorld.RowC };
        var titles = new List<string> { "x", "y" };

        run.Select<CorpusNote>(G, "lock-for-update", q => q.Where(n => n.Id == a).ForUpdate());
        run.Select<CorpusNote>(G, "lock-for-no-key-update", q => q.Where(n => n.Id == a).ForNoKeyUpdate());
        run.Select<CorpusNote>(G, "lock-for-update-with-order-limit-offset", q => q.Where(n => n.Rank > 1).OrderBy(n => n.Title).Take(2).Skip(1).ForUpdate());
        run.Select<CorpusNote>(G, "lock-for-no-key-update-with-order-limit", q => q.OrderByDescending(n => n.Rank).Take(1).ForNoKeyUpdate());
        run.Select<CorpusNote>(G, "lock-no-key-after-update-is-no-key", q => q.ForUpdate().ForNoKeyUpdate());
        run.Select<CorpusNote>(G, "lock-update-after-no-key-keeps-key-preserving", q => q.ForNoKeyUpdate().ForUpdate());
        run.Select<CorpusTask>(G, "lock-for-update-soft", q => q.Where(t => t.Rank > 1).ForUpdate());

        run.Update<CorpusNote>(G, "interval-now-plus-minutes", q => q.SetNowPlusMinutes(n => n.ExpiresAt, 30).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "interval-now-plus-negative-minutes", q => q.SetNowPlusMinutes(n => n.ExpiresAt, -5).Where(n => n.Id == a));
        run.Update<CorpusTask>(G, "interval-now-plus-minutes-soft", q => q.SetNowPlusMinutes(t => t.UpdatedAt, 1).Where(t => t.Id == a));

        run.Select<CorpusNote>(G, "any-of-guid-list", q => q.Where(n => Db.AnyOf(n.Id, ids)));
        run.Select<CorpusNote>(G, "any-of-guid-array", q => q.Where(n => Db.AnyOf(n.Id, array)));
        run.Select<CorpusNote>(G, "any-of-string-list-with-other-terms", q => q.Where(n => n.Rank > 1).Where(n => Db.AnyOf(n.Title, titles)).Where(n => n.Active));
        run.Select<CorpusTask>(G, "any-of-soft", q => q.Where(t => Db.AnyOf(t.Id, ids)));

        run.Select<CorpusNote>(G, "null-safe-equals-guid", q => q.Where(n => Db.NullSafeEquals(n.OwnerId, a)));
        run.Select<CorpusNote>(G, "null-safe-equals-null-value", q => q.Where(n => Db.NullSafeEquals(n.OwnerId, null)));
        run.Select<CorpusNote>(G, "null-safe-equals-string", q => q.Where(n => Db.NullSafeEquals(n.Body, "b")));

        run.Aggregates<CorpusNote>(G, "filter-aggregate-single", q => q.AggregateCountWhere(n => n.Active));
        run.Aggregates<CorpusNote>(G, "filter-aggregate-several-with-where", q => q.Where(n => n.Rank > 0).AggregateCountWhere(n => n.Active).AggregateCountWhere(n => n.Title == "a" || n.Title == "b"));
        run.Aggregates<CorpusNote>(G, "filter-aggregate-boolean-negated", q => q.AggregateCountWhere(n => !n.Active));
    }
}
