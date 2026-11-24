using System.Linq.Expressions;

namespace Neemle.OrmLite.Tests.Corpus.Cases;

internal static class QueryUpsertCases
{
    private const string G = "query-upsert";

    private const string M = "query-insert-many";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var b = CorpusWorld.RowB;
        var tenant = CorpusWorld.Tenant;
        var instant = CorpusWorld.Instant;

        Expression<Func<CorpusNote, object?>>[] noteId = [n => n.Id];
        Expression<Func<CorpusNote, object?>>[] noteKeys = [n => n.Id, n => n.Title];
        Expression<Func<CorpusTask, object?>>[] taskLive = [t => t.TenantId, t => t.Name];
        Expression<Func<CorpusTask, object?>>[] taskLiveReordered = [t => t.Name, t => t.TenantId];
        Expression<Func<CorpusTask, object?>>[] taskId = [t => t.Id];
        Expression<Func<CorpusPlatformItem, object?>>[] itemId = [i => i.Id];

        Row<CorpusNote> NoteRow() => new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Title, "t").With(n => n.Rank, 1);
        Row<CorpusTask> TaskRow() => new Row<CorpusTask>().With(t => t.Id, a).With(t => t.TenantId, tenant).With(t => t.Name, "n").With(t => t.Rank, 2);

        void Upsert<T>(string name, Func<CorpusRun, Query<T>> query, Row<T> row, IReadOnlyList<Expression<Func<T, object?>>> target, OnConflict action,
            IReadOnlyList<UpsertSet<T>>? sets = null, IReadOnlyList<Expression<Func<T, object?>>>? returning = null) where T : class =>
            run.Rec.Statement(G, name, () =>
            {
                var q = query(run);
                var rendered = q.RenderUpsert(row, target, action, sets, returning);
                return CorpusRun.Listed(q.CompileUpsert(row, target, action, sets, returning), rendered.Sql, rendered.Parameters);
            });

        Upsert("do-nothing-targeted", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoNothing);
        Upsert("do-nothing-untargeted", r => r.Q<CorpusNote>(), NoteRow(), [], OnConflict.DoNothing);
        Upsert("do-nothing-ignores-sets", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoNothing, [UpsertSet<CorpusNote>.Inserted(n => n.Title)]);
        Upsert("do-update-inserted-column", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Title)]);
        Upsert("do-update-several-inserted-columns", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Title), UpsertSet<CorpusNote>.Inserted(n => n.Rank)]);
        Upsert("do-update-increment", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Increment(n => n.Views, 3)]);
        Upsert("do-update-now", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Now(n => n.CreatedAt)]);
        Upsert("do-update-mixed-sets", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate,
            [UpsertSet<CorpusNote>.Inserted(n => n.Title), UpsertSet<CorpusNote>.Increment(n => n.Views, 1), UpsertSet<CorpusNote>.Now(n => n.CreatedAt), UpsertSet<CorpusNote>.Inserted(n => n.Rank)]);
        Upsert("do-update-multi-column-target", r => r.Q<CorpusNote>(), NoteRow(), noteKeys, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Rank)]);
        Upsert("do-update-with-returning", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoUpdate, [UpsertSet<CorpusNote>.Inserted(n => n.Title)], [n => n.Id, n => n.Title]);
        Upsert("do-nothing-with-returning", r => r.Q<CorpusNote>(), NoteRow(), noteId, OnConflict.DoNothing, null, [n => n.Id]);
        Upsert("clock-columns-render-database-now", r => r.Q<CorpusNote>(), NoteRow().WithNow(n => n.CreatedAt).WithNow(n => n.ExpiresAt), noteId, OnConflict.DoNothing);
        Upsert("row-with-jsonb-and-null-values", r => r.Q<CorpusNote>(),
            new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Payload, Jsonb.Parse("{\"k\":[1,2]}")).With(n => n.Body, null).With(n => n.Status, CorpusStatus.Draft), noteId, OnConflict.DoUpdate,
            [UpsertSet<CorpusNote>.Inserted(n => n.Payload), UpsertSet<CorpusNote>.Inserted(n => n.Body)]);
        Upsert("row-with-datetime-and-guid-values", r => r.Q<CorpusNote>(), new Row<CorpusNote>().With(n => n.Id, a).With(n => n.CreatedAt, instant).With(n => n.OwnerId, b), noteId, OnConflict.DoNothing);
        Upsert("soft-live-only-target-carries-partial-predicate", r => r.Q<CorpusTask>(), TaskRow(), taskLive, OnConflict.DoNothing);
        Upsert("soft-live-only-target-order-insensitive", r => r.Q<CorpusTask>(), TaskRow(), taskLiveReordered, OnConflict.DoNothing);
        Upsert("soft-live-only-target-do-update-guards-live-rows", r => r.Q<CorpusTask>(), TaskRow(), taskLive, OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Rank)]);
        Upsert("soft-key-target-do-update-guards-live-rows", r => r.Q<CorpusTask>(), TaskRow(), taskId, OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Name), UpsertSet<CorpusTask>.Now(t => t.UpdatedAt)]);
        Upsert("soft-key-target-do-nothing-has-no-guard", r => r.Q<CorpusTask>(), TaskRow(), taskId, OnConflict.DoNothing);
        Upsert("soft-do-update-with-returning", r => r.Q<CorpusTask>(), TaskRow(), taskId, OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Rank)], [t => t.Id]);
        Upsert("soft-platform-do-update", r => r.Q<CorpusPlatformItem>(), new Row<CorpusPlatformItem>().With(i => i.Id, a).With(i => i.Code, "c"), itemId, OnConflict.DoUpdate, [UpsertSet<CorpusPlatformItem>.Inserted(i => i.Code)]);

        void InsertMany<T>(string name, Query<T> query, IReadOnlyList<Row<T>> rows) where T : class =>
            run.Rec.Statement(M, name, () =>
            {
                var rendered = query.RenderInsertMany(rows);
                return CorpusRun.Listed(query.CompileInsertMany(rows), rendered.Sql, rendered.Parameters);
            });

        InsertMany("two-rows", run.Q<CorpusNote>(), [NoteRow(), new Row<CorpusNote>().With(n => n.Id, b).With(n => n.Title, "u").With(n => n.Rank, 2)]);
        InsertMany("single-row", run.Q<CorpusNote>(), [NoteRow()]);
        InsertMany("rows-with-clock-columns", run.Q<CorpusNote>(), [
            NoteRow().WithNow(n => n.CreatedAt),
            new Row<CorpusNote>().With(n => n.Id, b).With(n => n.Title, "u").With(n => n.Rank, 2).WithNow(n => n.CreatedAt)]);
        InsertMany("rows-with-jsonb-null-and-enum", run.Q<CorpusNote>(), [
            new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Payload, Jsonb.Parse("{}")).With(n => n.Body, null).With(n => n.Status, CorpusStatus.Active),
            new Row<CorpusNote>().With(n => n.Id, b).With(n => n.Payload, Jsonb.Parse("[1]")).With(n => n.Body, "x").With(n => n.Status, CorpusStatus.Draft)]);
        InsertMany("soft-entity-rows", run.Q<CorpusTask>(), [TaskRow(), new Row<CorpusTask>().With(t => t.Id, b).With(t => t.TenantId, tenant).With(t => t.Name, "m").With(t => t.Rank, 3)]);

        run.Rec.Text(M, "split-boundaries", () =>
        {
            ResolvedRow Resolved(int width) => new(Enumerable.Range(0, width).ToDictionary(i => "c" + i, i => (object?)i), []);
            var lines = new List<string>();
            foreach (var (rows, width, capacity) in new[] { (0, 3, 9), (1, 3, 9), (3, 3, 9), (4, 3, 9), (7, 3, 6), (5, 3, 7), (6, 1, 2), (2, 0, 5), (2, 3, 3) })
            {
                var input = Enumerable.Range(0, rows).Select(_ => Resolved(width)).ToList();
                var chunks = InsertManyChunks.Split(input, capacity);
                lines.Add("rows=" + rows + " width=" + width + " capacity=" + capacity + " -> " + chunks.Count + " chunk(s) of " + string.Join(", ", chunks.Select(x => x.Count)));
            }
            return CorpusFormat.Lines(lines);
        });
        run.Rec.Refusal(M, "split-row-wider-than-capacity", () => InsertManyChunks.Split([new ResolvedRow(new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2, ["c"] = 3 }, [])], 2));
    }
}
