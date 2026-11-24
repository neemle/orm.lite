namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class QueryWriteCases
{
    private const string G = "query-write";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var tenant = CorpusWorld.Tenant;
        var instant = CorpusWorld.Instant;

        run.Update<CorpusNote>(G, "update-set-string", q => q.Set(n => n.Title, "renamed").Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-null", q => q.Set(n => n.Body, null).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-int-long-double-decimal", q => q.Set(n => n.Rank, 4).Set(n => n.Views, 5L).Set(n => n.Score, 6.5).Set(n => n.Price, 7.25m).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-guid-and-bool", q => q.Set(n => n.OwnerId, b).Set(n => n.Active, true).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-enum", q => q.Set(n => n.Status, CorpusStatus.Archived).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-datetime-and-dateonly", q => q.Set(n => n.CreatedAt, instant).Set(n => n.Due, new DateOnly(2026, 7, 8)).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-jsonb-value-is-cast", q => q.Set(n => n.Payload, Jsonb.Parse("{\"k\":1}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-now", q => q.SetNow(n => n.CreatedAt).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-coalesce", q => q.SetCoalesce(n => n.Title, "fallback").Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-coalesce-jsonb", q => q.SetCoalesce(n => n.Payload, Jsonb.Parse("{}")).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-increment", q => q.SetIncrement(n => n.Rank, 3).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-increment-negative", q => q.SetIncrement(n => n.Rank, -1).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-column-from-column", q => q.SetColumn(n => n.Views, n => n.Rank).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-predicate", q => q.SetPredicate(n => n.Active, n => n.Rank > 3).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-set-predicate-parameter-order", q => q.SetPredicate(n => n.Active, n => n.Title == "p").Set(n => n.Rank, 1).Where(n => n.Id == a));
        run.Update<CorpusNote>(G, "update-many-sets-parameter-order", q => q.Where(n => n.Rank > 0).Set(n => n.Title, "t").SetCoalesce(n => n.Body, "b").SetIncrement(n => n.Views, 2).SetNow(n => n.CreatedAt).SetNowPlusMinutes(n => n.ExpiresAt, 15));
        run.Update<CorpusNote>(G, "update-where-before-and-after-sets", q => q.Where(n => n.Rank > 1).Set(n => n.Title, "x").Where(n => n.Title == "y"));
        run.Update<CorpusTask>(G, "update-soft-adds-live-term", q => q.Set(t => t.Name, "n").Where(t => t.TenantId == tenant));
        run.Update<CorpusTask>(G, "update-soft-including-deleted", q => q.IncludingDeleted().Set(t => t.Name, "n").Where(t => t.TenantId == tenant));

        run.Delete<CorpusNote>(G, "delete-physical", q => q.Where(n => n.Id == a));
        run.Delete<CorpusNote>(G, "delete-physical-several-terms", q => q.Where(n => n.Rank > 1).Where(n => n.Active));
        run.Delete<CorpusTask>(G, "delete-soft-becomes-update-with-live-term", q => q.Where(t => t.Id == a));
        run.Delete<CorpusTask>(G, "delete-soft-including-deleted-keeps-live-term", q => q.IncludingDeleted().Where(t => t.Id == a));
        run.Delete<CorpusPlatformItem>(G, "delete-soft-platform", q => q.Where(i => i.Code == "c"));

        run.Rec.Statement(G, "update-returning-columns", () =>
        {
            var q = run.Q<CorpusNote>().Set(n => n.Rank, 1).Where(n => n.Id == a);
            return CorpusRun.Read(q, q.CompileUpdateReturning([n => n.Id, n => n.Title]), q.RenderUpdateReturning([n => n.Id, n => n.Title]));
        });
        run.Rec.Statement(G, "update-returning-soft", () =>
        {
            var q = run.Q<CorpusTask>().Set(t => t.Rank, 1).Where(t => t.Id == a);
            return CorpusRun.Read(q, q.CompileUpdateReturning([t => t.Id]), q.RenderUpdateReturning([t => t.Id]));
        });
        run.Rec.Statement(G, "update-returning-no-columns", () =>
        {
            var q = run.Q<CorpusNote>().Set(n => n.Rank, 1).Where(n => n.Id == a);
            return CorpusRun.Read(q, q.CompileUpdateReturning([]), q.RenderUpdateReturning([]));
        });
        run.Rec.Statement(G, "delete-returning-columns", () =>
        {
            var q = run.Q<CorpusNote>().Where(n => n.Id == a);
            return CorpusRun.Read(q, q.CompileDeleteReturning([n => n.Id, n => n.Title]), q.RenderDeleteReturning([n => n.Id, n => n.Title]));
        });
        run.Rec.Statement(G, "delete-returning-soft", () =>
        {
            var q = run.Q<CorpusTask>().Where(t => t.Id == a);
            return CorpusRun.Read(q, q.CompileDeleteReturning([t => t.Id, t => t.Name]), q.RenderDeleteReturning([t => t.Id, t => t.Name]));
        });
        run.Rec.Statement(G, "delete-returning-soft-including-deleted", () =>
        {
            var q = run.Q<CorpusTask>().IncludingDeleted().Where(t => t.Id == a);
            return CorpusRun.Read(q, q.CompileDeleteReturning([t => t.Id]), q.RenderDeleteReturning([t => t.Id]));
        });
    }
}
