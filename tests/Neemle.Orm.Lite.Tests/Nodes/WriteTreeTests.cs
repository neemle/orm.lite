using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Corpus;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class WriteTreeTests
{
    private sealed class CapturingProvider : ISqlProvider
    {
        public Node? Last { get; private set; }

        public string Id => PostgresProvider.Instance.Id;

        public CapabilitySet Capabilities => PostgresProvider.Instance.Capabilities;

        public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();

        public Node Lower(Node tree)
        {
            Last = tree;
            return tree;
        }
    }

    private static readonly Guid A = CorpusWorld.RowA;

    private static readonly Guid B = CorpusWorld.RowB;

    private static readonly Guid Tenant = CorpusWorld.Tenant;

    private static Query<T> Of<T>(CapturingProvider capture) where T : class
    {
        CorpusWorld.Ensure();
        return new Query<T>(new OrmContext(new UnreachableDataSource(), capture), _ => throw new NotSupportedException("no rows are read here"));
    }

    private static ColumnRef Col(string name, Node? owner = null) => new(name, owner);

    private static Where Only(Node term) => new([term]);

    private static Node Built(Func<Query<CorpusNote>, Statement> compile)
    {
        var capture = new CapturingProvider();
        compile(Of<CorpusNote>(capture));
        return capture.Last!;
    }

    private static Node BuiltTask(Func<Query<CorpusTask>, Statement> compile)
    {
        var capture = new CapturingProvider();
        compile(Of<CorpusTask>(capture));
        return capture.Last!;
    }

    [Fact(DisplayName = "an update is an Update node: the assignments in the order they were written, each carrying its parameter ordinal, and the where terms numbered in call order around them")]
    public void An_update_is_a_tree_in_call_order()
    {
        var tree = Built(q => q.Where(n => n.Rank > 0).Set(n => n.Title, "t").SetNow(n => n.CreatedAt).CompileUpdate());

        Assert.Equal<Node>(
            new Update(
                new TableRef("corpus_notes"),
                [new Assignment(Col("title"), new Param(1, "t")), new Assignment(Col("created_at"), new ClockNow())],
                Only(new Compare(CompareOp.Greater, Col("rank"), new Param(0, 0)))),
            tree);
    }

    [Fact(DisplayName = "each assignment kind is its own node: coalesce, increment, interval, column copy, predicate and a JSON value under a cast")]
    public void Assignment_kinds_are_nodes()
    {
        var tree = (Update)Built(q => q
            .SetCoalesce(n => n.Title, "f")
            .SetIncrement(n => n.Rank, 3)
            .SetNowPlusMinutes(n => n.ExpiresAt, 15)
            .SetColumn(n => n.Views, n => n.Rank)
            .SetPredicate(n => n.Active, n => n.Rank > 3)
            .Set(n => n.Payload, Jsonb.Parse("{}"))
            .Where(n => n.Id == A)
            .CompileUpdate());

        Assert.Equal<NodeList>(
            [
                new Assignment(Col("title"), new Coalesce(new Param(0, "f"), Col("title"))),
                new Assignment(Col("rank"), new Plus(new Coalesce(Col("rank"), new IntLit(0)), new Param(1, 3))),
                new Assignment(Col("expires_at"), new ClockPlusMinutes(new Param(2, 15))),
                new Assignment(Col("views"), Col("rank")),
                new Assignment(Col("active"), new Group(new Compare(CompareOp.Greater, Col("rank"), new Param(3, 3)))),
                new Assignment(Col("payload"), new Cast(new Param(4, Jsonb.Parse("{}")), CastType.Json)),
            ],
            tree.Assignments);
    }

    [Fact(DisplayName = "a plain delete is a Delete node, a soft delete is an Update that stamps the clock, and both carry the live term; a purge is always a Delete without it")]
    public void Delete_shapes()
    {
        Assert.Equal<Node>(
            new Delete(new TableRef("corpus_notes"), Only(new Compare(CompareOp.Equal, Col("id"), new Param(0, A)))),
            Built(q => q.Where(n => n.Id == A).CompileDelete()));

        var live = new IsNull(Col("deleted_at"));
        var soft = new Update(
            new TableRef("corpus_tasks"),
            [new Assignment(Col("deleted_at"), new ClockNow())],
            new Where([live, new Compare(CompareOp.Equal, Col("id"), new Param(0, A))]));
        Assert.Equal<Node>(soft, BuiltTask(q => q.Where(t => t.Id == A).CompileDelete()));
        Assert.Equal<Node>(soft, BuiltTask(q => q.IncludingDeleted().Where(t => t.Id == A).CompileDelete()));
        Assert.Equal<Node>(
            new Delete(new TableRef("corpus_tasks"), Only(new Compare(CompareOp.Equal, Col("id"), new Param(0, A)))),
            BuiltTask(q => q.Where(t => t.Id == A).CompilePurge()));
    }

    [Fact(DisplayName = "a RETURNING list is a Returning node on the update or the delete, and an empty list leaves it off")]
    public void Returning_is_a_node()
    {
        var update = (Update)Built(q => q.Set(n => n.Rank, 1).Where(n => n.Id == A).CompileUpdateReturning([n => n.Id, n => n.Title]));
        var bare = (Update)Built(q => q.Set(n => n.Rank, 1).Where(n => n.Id == A).CompileUpdateReturning([]));
        var delete = (Delete)Built(q => q.Where(n => n.Id == A).CompileDeleteReturning([n => n.Id]));

        Assert.Equal(new Returning([Col("id"), Col("title")]), update.Returning);
        Assert.Null(bare.Returning);
        Assert.Equal(new Returning([Col("id")]), delete.Returning);
    }

    private static Row<CorpusNote> NoteRow() => new Row<CorpusNote>().With(n => n.Id, A).With(n => n.Title, "t");

    [Fact(DisplayName = "a targeted do-nothing upsert is an Insert with a Conflict that has no update, and an untargeted one has an empty target")]
    public void Upsert_do_nothing()
    {
        var values = new ValuesRow([new Param(0, A), new Param(1, "t")]);
        var columns = NodeList.Of([Col("id"), Col("title")]);

        Assert.Equal<Node>(
            new Insert(new TableRef("corpus_notes"), columns, [values], new Conflict([Col("id")])),
            Built(q => q.CompileUpsert(NoteRow(), [n => n.Id], OnConflict.DoNothing)));
        Assert.Equal<Node>(
            new Insert(new TableRef("corpus_notes"), columns, [values], new Conflict([])),
            Built(q => q.CompileUpsert(NoteRow(), [], OnConflict.DoNothing)));
    }

    [Fact(DisplayName = "a do-update upsert carries one assignment per set: a parameter, an increment of the "
        + "qualified column counted from zero when it is NULL, and the clock; with no sets it is refused")]
    public void Upsert_do_update()
    {
        var update = (Insert)Built(q => q.CompileUpsert(
            NoteRow(), [n => n.Id], OnConflict.DoUpdate,
            [UpsertSet<CorpusNote>.Inserted(n => n.Title), UpsertSet<CorpusNote>.Increment(n => n.Views, 3), UpsertSet<CorpusNote>.Now(n => n.CreatedAt)],
            [n => n.Id]));

        Assert.Equal(
            new Conflict(
                [Col("id")],
                null,
                new ConflictUpdate([
                    new Assignment(Col("title"), new Param(2, "t")),
                    new Assignment(Col("views"), new Plus(
                        new Coalesce(Col("views", new TableRef("corpus_notes")), new IntLit(0)), new IntLit(3))),
                    new Assignment(Col("created_at"), new ClockNow()),
                ])),
            update.Conflict);
        Assert.Equal(new Returning([Col("id")]), update.Returning);

        Assert.Throws<InvalidOperationException>(
            () => Built(q => q.CompileUpsert(NoteRow(), [n => n.Id], OnConflict.DoUpdate, [])));
    }

    [Fact(DisplayName = "a soft entity's upsert on its live-only target carries the live predicate on the target, and a do-update on it is guarded by the qualified live term")]
    public void Upsert_of_a_soft_entity()
    {
        var row = new Row<CorpusTask>().With(t => t.Id, A).With(t => t.TenantId, Tenant).With(t => t.Name, "n").With(t => t.Rank, 2);
        var capture = new CapturingProvider();

        Of<CorpusTask>(capture).CompileUpsert(row, [t => t.Name, t => t.TenantId], OnConflict.DoUpdate, [UpsertSet<CorpusTask>.Inserted(t => t.Rank)]);

        Assert.Equal(
            new Conflict(
                [Col("name"), Col("tenant_id")],
                new IsNull(Col("deleted_at")),
                new ConflictUpdate(
                    [new Assignment(Col("rank"), new Param(4, 2))],
                    new IsNull(Col("deleted_at", new TableRef("corpus_tasks"))))),
            ((Insert)capture.Last!).Conflict);
    }

    [Fact(DisplayName = "a set that a do-nothing upsert never uses is still bound in the list the render returns, and the compiled statement carries only the parameters its text names")]
    public void An_unused_set_is_bound_but_not_compiled()
    {
        var capture = new CapturingProvider();
        var query = Of<CorpusNote>(capture);
        IReadOnlyList<UpsertSet<CorpusNote>> sets = [UpsertSet<CorpusNote>.Inserted(n => n.Title)];

        var rendered = query.RenderUpsert(NoteRow(), [n => n.Id], OnConflict.DoNothing, sets);
        var compiled = query.CompileUpsert(NoteRow(), [n => n.Id], OnConflict.DoNothing, sets);

        Assert.Equal(3, rendered.Parameters.Count);
        Assert.Equal(new object?[] { A, "t" }, compiled.Values);
        Assert.Equal(new[] { "p0", "p1" }, compiled.Parameters.Select(p => p.Name));
    }

    [Fact(DisplayName = "an insert-many is one Insert whose rows number their parameters straight on from the first, with the clock columns last in every row")]
    public void Insert_many_numbers_across_rows()
    {
        var capture = new CapturingProvider();
        Row<CorpusNote> Row(Guid id, string title) => new Row<CorpusNote>().With(n => n.Id, id).With(n => n.Title, title).WithNow(n => n.CreatedAt);

        Of<CorpusNote>(capture).CompileInsertMany([Row(A, "t"), Row(B, "u")]);

        Assert.Equal<Node>(
            new Insert(
                new TableRef("corpus_notes"),
                [Col("id"), Col("title"), Col("created_at")],
                [
                    new ValuesRow([new Param(0, A), new Param(1, "t"), new ClockNow()]),
                    new ValuesRow([new Param(2, B), new Param(3, "u"), new ClockNow()]),
                ]),
            capture.Last!);
    }

    [Fact(DisplayName = "a restore is an Update that nulls the soft column where the key is in the array, the column is set and the tenant matches; a purge by key is a Delete with the same key and tenant terms")]
    public void Key_statements_are_trees()
    {
        var ids = new List<Guid> { A, B };
        var capture = new CapturingProvider();

        Of<CorpusTask>(capture).Where(t => t.Rank > 1).CompileRestore(ids, Tenant);
        Assert.Equal<Node>(
            new Update(
                new TableRef("corpus_tasks"),
                [new Assignment(Col("deleted_at"), new NullLit())],
                new Where([
                    new AnyOf(Col("id"), new Param(1, new[] { A, B })),
                    new IsNotNull(Col("deleted_at")),
                    new Compare(CompareOp.Equal, Col("tenant_id"), new Param(2, Tenant)),
                ])),
            capture.Last!);

        Of<CorpusPlatformItem>(capture).CompilePurge(ids, null);
        Assert.Equal<Node>(
            new Delete(new TableRef("corpus_platform_items"), Only(new AnyOf(Col("id"), new Param(0, new[] { A, B })))),
            capture.Last!);
    }

    [Fact(DisplayName = "a JSON merge, a remove-then-merge and a member increment are trees of the document nodes, with the cleared keys bound before the patch")]
    public void Json_writes_are_trees()
    {
        var legacy = new MovedFieldKey("legacy_name");
        var other = new MovedFieldKey("other_field");
        var patch = Jsonb.Parse("{\"a\":1}");
        var empty = new JsonDocumentOrEmpty(Col("meta"));

        var merge = (Update)Built(q => q.SetJsonMerge(n => n.Meta, patch).Where(n => n.Id == A).CompileUpdate());
        var removed = (Update)Built(q => q.SetJsonRemoveThenMerge(n => n.Meta, [legacy, other], [], patch).Where(n => n.Id == A).CompileUpdate());
        var increment = (Update)Built(q => q.SetJsonMemberIncrement(n => n.Meta, legacy, 4).Where(n => n.Id == A).CompileUpdate());

        Assert.Equal<NodeList>(
            [new Assignment(Col("meta"), new JsonConcat(empty, new Cast(new Param(0, patch), CastType.Json)))], merge.Assignments);
        Assert.Equal<NodeList>(
            [new Assignment(
                Col("meta"),
                new JsonConcat(
                    new JsonRemoveKey(new JsonRemoveKey(empty, new Param(0, "legacy_name")), new Param(1, "other_field")),
                    new Cast(new Param(2, patch), CastType.Json)))],
            removed.Assignments);
        Assert.Equal<NodeList>(
            [new Assignment(
                Col("meta"),
                new JsonConcat(
                    empty,
                    new JsonObjectOf(
                        new Param(0, "legacy_name"),
                        new Plus(new JsonIntOrZero(new JsonKeyText(Col("meta"), new Param(0, "legacy_name"))), new Param(1, 4)))))],
            increment.Assignments);
    }
}
