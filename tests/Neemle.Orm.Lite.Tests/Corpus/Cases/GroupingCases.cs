using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class GroupingCases
{
    private const string G = "query-read";

    public static void Register(CorpusRun run)
    {
        run.Aggregates<CorpusNote>(G, "aggregate-max", q => q.AggregateMax(n => n.CreatedAt));
        run.Aggregates<CorpusNote>(G, "aggregate-avg", q => q.AggregateAvg(n => n.Rank));
        run.Aggregates<CorpusNote>(G, "aggregate-decimal-sum-min-max-and-exact-average-with-where", q =>
            q.Where(n => n.Active).AggregateSum(n => n.Price).AggregateMin(n => n.Price).AggregateMax(n => n.Price).AggregateAvgDecimal(n => n.Price, 2));

        Grouped(run, "group-by-one-column-with-every-aggregate", q =>
        {
            var g = q.GroupBy(n => n.Status);
            g.Count();
            g.CountDistinct(n => n.OwnerId);
            g.CountWhere(n => n.Rank > 3);
            g.Sum<long>(n => n.Views);
            g.Min<DateTime?>(n => n.CreatedAt);
            g.Max<DateTime?>(n => n.CreatedAt);
            g.Avg(n => n.Rank);
            return g.CompileSelect();
        });
        Grouped(run, "group-by-a-decimal-column-with-exact-aggregates-having-and-order", q =>
        {
            var g = q.GroupBy(n => n.Status);
            var sum = g.Sum<decimal>(n => n.Price);
            g.Min<decimal?>(n => n.Price);
            g.Max<decimal?>(n => n.Price);
            g.AvgDecimal(n => n.Price, 2);
            g.Having(sum, CompareOp.GreaterOrEqual, 9.99m).OrderBy(sum, descending: true);
            return g.CompileSelect();
        });
        Grouped(run, "group-by-two-columns", q =>
        {
            var g = q.GroupBy(n => n.Status, n => n.Active);
            g.Count();
            return g.CompileSelect();
        });
        Grouped(run, "group-by-three-columns", q =>
        {
            var g = q.GroupBy(n => n.Status, n => n.Active, n => n.Title);
            g.Count();
            return g.CompileSelect();
        });
        Grouped(run, "group-by-key-only", q => q.GroupBy(n => n.Title).CompileSelect());
        Grouped(run, "group-by-with-where-exists-and-in", q =>
        {
            var g = q.Where(n => n.Rank > 1)
                .WhereExists<CorpusTag>((n, t) => t.NoteId == n.Id)
                .WhereIn<CorpusTag>(n => n.Id, sub => sub.Where(t => t.Label == "l"), t => t.NoteId)
                .GroupBy(n => n.Status);
            g.Count();
            return g.CompileSelect();
        });
        Grouped(run, "group-by-having-and-order-by-aggregate", q =>
        {
            var g = q.GroupBy(n => n.Rank);
            var count = g.Count();
            var views = g.Sum<long>(n => n.Views);
            g.Having(count, CompareOp.Greater, 1L).Having(views, CompareOp.LessOrEqual, 900L)
                .OrderBy(count, descending: true).OrderByKey().Take(5).Skip(10);
            return g.CompileSelect();
        });
        Grouped(run, "group-by-having-on-a-filtered-count", q =>
        {
            var g = q.GroupBy(n => n.Title);
            var active = g.CountWhere(n => n.Active);
            g.Having(active, CompareOp.GreaterOrEqual, 2L).OrderByKey(descending: true);
            return g.CompileSelect();
        });
        run.Rec.Statement(G, "group-by-soft-live", () =>
        {
            var q = run.Q<CorpusTask>();
            var g = q.GroupBy(t => t.ProjectId);
            g.Count();
            return CorpusRun.Read(q, g.CompileSelect(), g.RenderSelect());
        });
        run.Rec.Statement(G, "group-by-soft-including-deleted", () =>
        {
            var q = run.Q<CorpusTask>().IncludingDeleted();
            var g = q.GroupBy(t => t.ProjectId);
            g.Count();
            return CorpusRun.Read(q, g.CompileSelect(), g.RenderSelect());
        });

        run.Rec.Refusal("refusals", "group-by-count-where-without-the-filter-capability", () =>
            run.QNoCap<CorpusNote>().GroupBy(n => n.Status).CountWhere(n => n.Active));
        run.Rec.Refusal("refusals", "group-by-having-compares-to-null", () =>
        {
            var g = run.Q<CorpusNote>().GroupBy(n => n.Status);
            g.Having(g.Min<string>(n => n.Title), CompareOp.Equal, null!);
        });
        run.Rec.Refusal("refusals", "aggregate-avg-of-a-decimal-column", () =>
            run.Q<CorpusNote>().AggregateAvg(n => n.Price));
        run.Rec.Refusal("refusals", "group-by-avg-of-a-decimal-column", () =>
            run.Q<CorpusNote>().GroupBy(n => n.Status).Avg(n => n.Price));
        run.Rec.Refusal("refusals", "group-by-aggregate-of-another-grouping", () =>
        {
            var other = run.Q<CorpusNote>().GroupBy(n => n.Status).Count();
            run.Q<CorpusNote>().GroupBy(n => n.Status).OrderBy(other);
        });
    }

    private static void Grouped(CorpusRun run, string name, Func<Query<CorpusNote>, Statement> build) =>
        run.Rec.Statement(G, name, () =>
        {
            var q = run.Q<CorpusNote>();
            var statement = build(q);
            return CorpusRun.Read(q, statement, statement.Text);
        });
}
