namespace Neemle.OrmLite.Tests.Corpus.Cases;

internal static class QueryReadCases
{
    private const string G = "query-read";

    private sealed class Holder
    {
        public int Rank = 5;

        public Holder? Inner;

        public string Title { get; } = "held";

        public CorpusStatus Kind = CorpusStatus.Archived;

        public int[] Arr = [10, 20, 30];
    }

    private readonly struct Wrapper(int value)
    {
        public int Value { get; } = value;

        public static implicit operator int(Wrapper w) => w.Value + 100;
    }

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var c = CorpusWorld.RowC;
        var tenant = CorpusWorld.Tenant;
        var instant = CorpusWorld.Instant;
        var due = new DateOnly(2026, 5, 6);

        run.Select<CorpusNote>(G, "select-all-plain", q => q);
        run.Rec.Statement(G, "select-render-is-repeatable-and-exposes-the-where-terms", () =>
        {
            var q = run.Q<CorpusNote>().Where(n => n.Rank > 1).Where(n => n.Title == "w");
            var first = q.RenderSelect();
            var second = q.RenderSelect();
            return (first + "\n-- second render equal: " + (first == second) + "\n-- where terms: " + string.Join(" | ", q.WhereClauses), q.BoundParameters.ToList());
        });
        run.Select<CorpusTask>(G, "select-all-soft-live", q => q);
        run.Select<CorpusTask>(G, "select-all-soft-including-deleted", q => q.IncludingDeleted());
        run.Select<CorpusPlatformItem>(G, "select-all-soft-platform", q => q);

        run.Select<CorpusNote>(G, "where-string-equals", q => q.Where(n => n.Title == "alpha"));
        run.Select<CorpusNote>(G, "where-int-not-equal", q => q.Where(n => n.Rank != 3));
        run.Select<CorpusNote>(G, "where-int-less", q => q.Where(n => n.Rank < 3));
        run.Select<CorpusNote>(G, "where-int-less-or-equal", q => q.Where(n => n.Rank <= 3));
        run.Select<CorpusNote>(G, "where-int-greater", q => q.Where(n => n.Rank > 3));
        run.Select<CorpusNote>(G, "where-int-greater-or-equal", q => q.Where(n => n.Rank >= 3));
        run.Select<CorpusNote>(G, "where-long-and-double-and-decimal", q => q.Where(n => n.Views == 7L && n.Score >= 1.5 && n.Price < 9.99m));
        run.Select<CorpusNote>(G, "where-reversed-operands", q => q.Where(n => 3 < n.Rank));
        run.Select<CorpusNote>(G, "where-reversed-operands-less-or-equal", q => q.Where(n => 3 <= n.Rank));
        run.Select<CorpusNote>(G, "where-reversed-operands-greater", q => q.Where(n => 3 > n.Rank));
        run.Select<CorpusNote>(G, "where-reversed-operands-greater-or-equal", q => q.Where(n => 3 >= n.Rank));
        run.Select<CorpusNote>(G, "where-constant-left-equals", q => q.Where(n => 3 == n.Rank));
        run.Select<CorpusNote>(G, "where-constant-left-not-equals", q => q.Where(n => 3 != n.Rank));
        run.Select<CorpusNote>(G, "where-null-equals", q => q.Where(n => n.Body == null));
        run.Select<CorpusNote>(G, "where-not-null", q => q.Where(n => n.Body != null));
        run.Select<CorpusNote>(G, "where-guid-equals", q => q.Where(n => n.Id == a));
        run.Select<CorpusNote>(G, "where-nullable-guid-equals", q => q.Where(n => n.OwnerId == a));
        run.Select<CorpusNote>(G, "where-nullable-guid-null", q => q.Where(n => n.OwnerId == null));
        run.Select<CorpusNote>(G, "where-bool-column", q => q.Where(n => n.Active));
        run.Select<CorpusNote>(G, "where-bool-column-negated", q => q.Where(n => !n.Active));
        run.Select<CorpusNote>(G, "where-bool-equals-constant", q => q.Where(n => n.Active == true));
        run.Select<CorpusNote>(G, "where-enum-equals", q => q.Where(n => n.Status == CorpusStatus.Active));
        run.Select<CorpusNote>(G, "where-nullable-enum-equals", q => q.Where(n => n.Review == CorpusStatus.Archived));
        run.Select<CorpusNote>(G, "where-enum-compared-as-integer", q => q.Where(n => (int)n.Status == 2));
        run.Select<CorpusNote>(G, "where-datetime-and-dateonly", q => q.Where(n => n.CreatedAt >= instant && n.Due == due));
        run.Select<CorpusNote>(G, "where-and-or-not-nesting", q => q.Where(n => (n.Rank > 1 || n.Title == "x") && !(n.Views < 3) && (n.Active || !n.Active)));
        run.Select<CorpusNote>(G, "where-not-of-comparison", q => q.Where(n => !(n.Rank == 4)));
        run.Select<CorpusNote>(G, "where-column-against-column", q => q.Where(n => n.CreatedAt <= n.ExpiresAt));
        run.Select<CorpusNote>(G, "where-column-equals-column", q => q.Where(n => n.Id == n.OwnerId));
        run.Select<CorpusNote>(G, "where-database-clock-right", q => q.Where(n => n.ExpiresAt > Db.Now()));
        run.Select<CorpusNote>(G, "where-database-clock-left", q => q.Where(n => Db.Now() >= n.ExpiresAt));
        var ids = new List<Guid> { a, b, c };
        var none = new List<Guid>();
        var titles = new[] { "one", "two" };
        run.Select<CorpusNote>(G, "where-in-list", q => q.Where(n => ids.Contains(n.Id)));
        run.Select<CorpusNote>(G, "where-in-empty-list", q => q.Where(n => none.Contains(n.Id)));
        run.Select<CorpusNote>(G, "where-in-array", q => q.Where(n => titles.Contains(n.Title)));
        run.Select<CorpusNote>(G, "where-in-after-other-parameters", q => q.Where(n => n.Rank > 1).Where(n => titles.Contains(n.Title)).Where(n => n.Title != "z"));
        run.Select<CorpusNote>(G, "where-like-contains", q => q.Where(n => n.Title.Contains("a_b%c~d")));
        run.Select<CorpusNote>(G, "where-like-starts-with", q => q.Where(n => n.Title.StartsWith("pre")));
        run.Select<CorpusNote>(G, "where-like-ends-with", q => q.Where(n => n.Title.EndsWith("suf")));
        run.Select<CorpusNote>(G, "where-like-contains-ignore-case", q => q.Where(n => n.Title.Contains("MiXed", StringComparison.OrdinalIgnoreCase)));
        run.Select<CorpusNote>(G, "where-like-starts-with-ignore-case", q => q.Where(n => n.Title.StartsWith("Pre", StringComparison.InvariantCultureIgnoreCase)));
        run.Select<CorpusNote>(G, "where-like-ends-with-case-sensitive-comparison", q => q.Where(n => n.Title.EndsWith("Suf", StringComparison.Ordinal)));
        run.Select<CorpusNote>(G, "where-equals-ignore-case", q => q.Where(n => Db.EqualsIgnoreCase(n.Title, "MiXed")));
        var holder = new Holder { Inner = new Holder { Rank = 9 } };
        run.Select<CorpusNote>(G, "where-captured-field", q => q.Where(n => n.Rank == holder.Rank));
        run.Select<CorpusNote>(G, "where-captured-nested-field", q => q.Where(n => n.Rank == holder.Inner!.Rank));
        run.Select<CorpusNote>(G, "where-captured-property", q => q.Where(n => n.Title == holder.Title));
        ClosureValues(run, holder);
        run.Select<CorpusNote>(G, "where-two-calls-are-anded", q => q.Where(n => n.Rank > 1).Where(n => n.Title == "x"));
        run.Select<CorpusTask>(G, "where-soft-live-term-then-predicates", q => q.Where(t => t.TenantId == tenant).Where(t => t.Rank >= 2));
        run.Select<CorpusTask>(G, "where-soft-including-deleted-then-predicate", q => q.IncludingDeleted().Where(t => t.Name == "n"));

        run.Select<CorpusNote>(G, "order-by-ascending", q => q.OrderBy(n => n.Title));
        run.Select<CorpusNote>(G, "order-by-descending", q => q.OrderByDescending(n => n.Rank));
        run.Select<CorpusNote>(G, "order-then-by-chain", q => q.OrderBy(n => n.Title).ThenByDescending(n => n.Rank).ThenBy(n => n.Id));
        run.Select<CorpusNote>(G, "order-by-replaces-earlier-order", q => q.OrderBy(n => n.Title).ThenBy(n => n.Rank).OrderByDescending(n => n.Views));
        run.Select<CorpusNote>(G, "order-by-predicate", q => q.OrderByPredicate(n => n.Rank > 3));
        run.Select<CorpusNote>(G, "order-then-by-predicate", q => q.OrderBy(n => n.Title).ThenByPredicate(n => n.Active));
        run.Select<CorpusNote>(G, "order-by-predicate-with-parameter-before-where-parameter", q => q.OrderByPredicate(n => n.Title == "first").Where(n => n.Title == "second"));

        run.Select<CorpusNote>(G, "limit-take", q => q.Take(10));
        run.Select<CorpusNote>(G, "limit-skip", q => q.Skip(20));
        run.Select<CorpusNote>(G, "limit-take-and-skip", q => q.Take(10).Skip(20));
        run.Select<CorpusNote>(G, "limit-zero", q => q.Take(0).Skip(0));
        run.Select<CorpusTask>(G, "limit-full-statement-soft", q => q.Where(t => t.Rank > 1).OrderByDescending(t => t.Rank).ThenBy(t => t.Name).Take(5).Skip(10));

        run.Project<CorpusNote>(G, "project-columns", q => q, n => n.Id, n => n.Title);
        run.Project<CorpusTask>(G, "project-columns-soft-where-order", q => q.Where(t => t.Rank > 0).OrderBy(t => t.Name).Take(3), t => t.Id, t => t.Rank);
        run.Projection<CorpusNote>(G, "projection-column-terms", q => q.ProjectColumn(n => n.Id).ProjectColumn(n => n.Title));
        run.Projection<CorpusNote>(G, "projection-predicate-term", q => q.ProjectColumn(n => n.Id).ProjectPredicate(n => n.Rank > 3 && n.Active).Where(n => n.Title == "x"));
        run.Projection<CorpusNote>(G, "projection-predicate-parameter-order", q => q.ProjectPredicate(n => n.Title == "projected").Where(n => n.Title == "filtered"));

        run.Select<CorpusNote>(G, "redact-one-column", q => q.Redact(n => n.Body));
        run.Select<CorpusNote>(G, "redact-conditional", q => q.RedactWhen(n => n.Body, n => n.Active));
        run.Select<CorpusNote>(G, "redact-conditional-parameter-order", q => q.RedactWhen(n => n.Body, n => n.Title == "red").Where(n => n.Title == "filter"));
        run.Select<CorpusTask>(G, "redact-soft-with-where", q => q.Redact(t => t.Status).Where(t => t.Rank == 1));

        run.Aggregates<CorpusNote>(G, "aggregate-count", q => q.AggregateCount());
        run.Aggregates<CorpusNote>(G, "aggregate-count-distinct", q => q.AggregateCountDistinct(n => n.Title));
        run.Aggregates<CorpusNote>(G, "aggregate-sum", q => q.AggregateSum(n => n.Views));
        run.Aggregates<CorpusNote>(G, "aggregate-min", q => q.AggregateMin(n => n.CreatedAt));
        run.Aggregates<CorpusNote>(G, "aggregate-count-where", q => q.AggregateCountWhere(n => n.Rank > 3));
        run.Aggregates<CorpusNote>(G, "aggregate-all-with-where", q => q.Where(n => n.Active).AggregateCount().AggregateCountDistinct(n => n.OwnerId).AggregateSum(n => n.Rank).AggregateMin(n => n.Rank).AggregateCountWhere(n => n.Title == "a").AggregateCountWhere(n => n.Title == "b"));
        run.Aggregates<CorpusTask>(G, "aggregate-soft-live", q => q.AggregateCount().AggregateCountWhere(t => t.Rank > 1));
        run.Aggregates<CorpusTask>(G, "aggregate-soft-including-deleted", q => q.IncludingDeleted().AggregateCount());

        run.Select<CorpusNote>(G, "subquery-where-in", q => q.WhereIn<CorpusTag>(n => n.Id, sub => sub.Where(t => t.Position > 1), t => t.NoteId));
        run.Select<CorpusNote>(G, "subquery-where-in-parameter-offset", q => q.Where(n => n.Rank > 1).Where(n => n.Title == "x").WhereIn<CorpusTag>(n => n.Id, sub => sub.Where(t => t.Label == "l").Where(t => t.Position < 9), t => t.NoteId).Where(n => n.Views > 2));
        run.Select<CorpusNote>(G, "subquery-where-in-without-inner-where", q => q.WhereIn<CorpusTag>(n => n.Id, sub => sub, t => t.NoteId));
        run.Select<CorpusTask>(G, "subquery-where-in-soft-outer-and-inner", q => q.WhereIn<CorpusTask>(t => t.ProjectId, sub => sub.Where(s => s.Rank > 2), s => s.ProjectId));
        run.Select<CorpusNote>(G, "subquery-where-in-nested", q => q.WhereIn<CorpusTag>(n => n.Id, sub => sub.WhereIn<CorpusNote>(t => t.NoteId, inner => inner.Where(i => i.Rank > 5), i => i.Id), t => t.NoteId));
        run.Select<CorpusNote>(G, "subquery-where-exists", q => q.WhereExists<CorpusTag>((n, t) => t.NoteId == n.Id));
        run.Select<CorpusNote>(G, "subquery-where-exists-with-inner-value", q => q.Where(n => n.Rank > 1).WhereExists<CorpusTag>((n, t) => t.NoteId == n.Id && t.Position > 3));
        run.Select<CorpusNote>(G, "subquery-where-exists-soft-other-adds-live-term", q => q.WhereExists<CorpusTask>((n, t) => t.ProjectId == n.OwnerId));
        run.Select<CorpusNote>(G, "subquery-two-where-exists-aliases", q => q.WhereExists<CorpusTag>((n, t) => t.NoteId == n.Id).WhereExists<CorpusTask>((n, t) => t.ProjectId == n.OwnerId));
        run.Select<CorpusNote>(G, "subquery-db-exists-marker", q => q.Where(n => Db.Exists<CorpusTag>(t => t.NoteId == n.Id)));
        run.Select<CorpusNote>(G, "subquery-db-exists-with-outer-predicate", q => q.Where(n => n.Rank > 1 && Db.Exists<CorpusTag>(t => t.NoteId == n.Id && t.Label == "x")));
        run.Select<CorpusNote>(G, "subquery-db-exists-soft-other", q => q.Where(n => Db.Exists<CorpusTask>(t => t.ProjectId == n.OwnerId)));
        run.Select<CorpusNote>(G, "subquery-db-exists-negated", q => q.Where(n => !Db.Exists<CorpusTag>(t => t.NoteId == n.Id)));
        run.Select<CorpusTask>(G, "subquery-db-exists-inside-soft-outer", q => q.Where(t => Db.Exists<CorpusNote>(n => n.OwnerId == t.ProjectId)));
    }

    private static void ClosureValues(CorpusRun run, Holder holder)
    {
        var limit = 5; var dm = -1.5m; var flag = false; var iv = 2; var dv = 2.7; var lg = 3L;
        // exact in binary: the inexact 2.7d converts to different decimals on different runtimes (.NET 10 and 11)
        var dx = 2.5;
        var wrapper = new Wrapper(7); object text = "held"; int? niv = 4;

        run.Select<CorpusNote>(G, "where-closure-negated-int", q => q.Where(n => n.Rank > -limit));
        run.Select<CorpusNote>(G, "where-closure-negated-decimal", q => q.Where(n => n.Price < -dm));
        run.Select<CorpusNote>(G, "where-closure-not-bool", q => q.Where(n => n.Active == !flag));
        run.Select<CorpusNote>(G, "where-closure-double-not-bool", q => q.Where(n => n.Active == !!flag));
        run.Select<CorpusNote>(G, "where-closure-ones-complement", q => q.Where(n => n.Rank == ~iv));
        run.Select<CorpusNote>(G, "where-closure-narrowing-double-to-int", q => q.Where(n => n.Rank == (int)dv));
        run.Select<CorpusNote>(G, "where-closure-narrowing-long-to-int", q => q.Where(n => n.Rank == (int)lg));
        run.Select<CorpusNote>(G, "where-closure-enum-cast-to-int", q => q.Where(n => n.Rank == (int)holder.Kind));
        run.Select<CorpusNote>(G, "where-closure-double-to-decimal", q => q.Where(n => n.Price == (decimal)dx));
        run.Select<CorpusNote>(G, "where-closure-widening-int-to-long", q => q.Where(n => n.Views == iv));
        run.Select<CorpusNote>(G, "where-closure-widening-int-to-double", q => q.Where(n => n.Score == iv));
        run.Select<CorpusNote>(G, "where-closure-checked-negate", q => q.Where(n => n.Rank > checked(-limit)));
        run.Select<CorpusNote>(G, "where-closure-checked-cast", q => q.Where(n => n.Rank == checked((int)lg)));
        run.Select<CorpusNote>(G, "where-closure-array-length", q => q.Where(n => n.Rank == holder.Arr.Length));
        run.Select<CorpusNote>(G, "where-closure-user-defined-conversion", q => q.Where(n => n.Rank == wrapper));
        run.Select<CorpusNote>(G, "where-closure-as-cast", q => q.Where(n => n.Title == (text as string)));
        run.Select<CorpusNote>(G, "where-closure-negated-value-on-the-left", q => q.Where(n => -limit < n.Rank));
        run.Select<CorpusNote>(G, "where-closure-nullable-lift-unchanged", q => q.Where(n => n.Rank == niv));
    }
}
