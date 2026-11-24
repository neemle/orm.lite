using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Corpus;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class QueryTreeTests
{
    private static OrmContext Context(ISqlProvider? provider = null) =>
        new(new UnreachableDataSource(), provider ?? PostgresProvider.Instance, new PgDialect());

    private static Query<T> Of<T>(ISqlProvider? provider = null) where T : class
    {
        CorpusWorld.Ensure();
        return new Query<T>(Context(provider), _ => throw new NotSupportedException("no rows are read here"));
    }

    private static ColumnRef Col(string name, Node? owner = null) => new(name, owner);

    [Fact(DisplayName = "Where translates a lambda into a tree: the operands, the operator and the parameter ordinals in the order the terms were written")]
    public void Where_builds_the_expected_tree()
    {
        var query = Of<CorpusNote>().Where(n => n.Rank > 1 && n.Title == "x");

        Assert.Equal(
            [new And(
                new Compare(CompareOp.Greater, Col("rank"), new Param(0, 1)),
                new Compare(CompareOp.Equal, Col("title"), new Param(1, "x")))],
            query.WhereTerms);
    }

    [Fact(DisplayName = "a comparison with null becomes IS NULL or IS NOT NULL, a bare boolean column compares with true, and a negated one with false")]
    public void Null_and_boolean_forms_are_nodes()
    {
        Assert.Equal<Node>(new IsNull(Col("body")), Of<CorpusNote>().Where(n => n.Body == null).WhereTerms.Single());
        Assert.Equal<Node>(new IsNotNull(Col("body")), Of<CorpusNote>().Where(n => n.Body != null).WhereTerms.Single());
        Assert.Equal<Node>(new Compare(CompareOp.Equal, Col("active"), new BoolLit(true)), Of<CorpusNote>().Where(n => n.Active).WhereTerms.Single());
        Assert.Equal<Node>(new Compare(CompareOp.Equal, Col("active"), new BoolLit(false)), Of<CorpusNote>().Where(n => !n.Active).WhereTerms.Single());
        Assert.Equal<Node>(
            new Not(new Compare(CompareOp.Equal, Col("rank"), new Param(0, 4))),
            Of<CorpusNote>().Where(n => !(n.Rank == 4)).WhereTerms.Single());
    }

    [Fact(DisplayName = "Contains becomes a list membership with one parameter per element, and an empty list becomes the constant false")]
    public void Contains_becomes_a_membership()
    {
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var ids = new List<Guid> { one, two };
        var none = new List<Guid>();

        Assert.Equal<Node>(
            new InList(Col("id"), [new Param(0, one), new Param(1, two)]),
            Of<CorpusNote>().Where(n => ids.Contains(n.Id)).WhereTerms.Single());
        Assert.Equal<Node>(new BoolLit(false), Of<CorpusNote>().Where(n => none.Contains(n.Id)).WhereTerms.Single());
    }

    [Fact(DisplayName = "string matching becomes a LIKE node with the escaped pattern as its parameter, and the ignore-case overload becomes ILIKE")]
    public void String_matching_becomes_like_nodes()
    {
        Assert.Equal<Node>(
            new Like(Col("title"), new Param(0, "%a~_b~%%"), '~'),
            Of<CorpusNote>().Where(n => n.Title.Contains("a_b%")).WhereTerms.Single());
        Assert.Equal<Node>(
            new ILike(Col("title"), new Param(0, "pre%"), '~'),
            Of<CorpusNote>().Where(n => n.Title.StartsWith("pre", StringComparison.OrdinalIgnoreCase)).WhereTerms.Single());
        Assert.Equal<Node>(
            new Compare(CompareOp.Equal, new Call(Fn.Lower, [Col("title")]), new Call(Fn.Lower, [new Param(0, "X")])),
            Of<CorpusNote>().Where(n => Db.EqualsIgnoreCase(n.Title, "X")).WhereTerms.Single());
    }

    [Fact(DisplayName = "the database clock marker becomes the clock node on whichever side it stands")]
    public void The_clock_marker_becomes_a_node()
    {
        Assert.Equal<Node>(
            new Compare(CompareOp.Greater, Col("expires_at"), new ClockNow()),
            Of<CorpusNote>().Where(n => n.ExpiresAt > Db.Now()).WhereTerms.Single());
        Assert.Equal<Node>(
            new Compare(CompareOp.GreaterOrEqual, new ClockNow(), Col("expires_at")),
            Of<CorpusNote>().Where(n => Db.Now() >= n.ExpiresAt).WhereTerms.Single());
    }

    [Fact(DisplayName = "Db.Exists becomes a correlated select: the inner columns carry the alias, the outer one its table, and a soft-deletable inner entity adds its live term")]
    public void Exists_becomes_a_correlated_select()
    {
        var plain = Of<CorpusNote>().Where(n => Db.Exists<CorpusTag>(t => t.NoteId == n.Id)).WhereTerms.Single();
        var soft = Of<CorpusNote>().Where(n => Db.Exists<CorpusTask>(t => t.ProjectId == n.OwnerId)).WhereTerms.Single();

        Assert.Equal<Node>(
            new Exists(new Select([new IntLit(1)], new TableRef("corpus_tags", "s0"),
                new Where([new Compare(CompareOp.Equal, Col("note_id", new AliasRef("s0")), Col("id", new TableRef("corpus_notes")))]))),
            plain);
        Assert.Equal<Node>(
            new Exists(new Select([new IntLit(1)], new TableRef("corpus_tasks", "s0"),
                new Where([
                    new Compare(CompareOp.Equal, Col("project_id", new AliasRef("s0")), Col("owner_id", new TableRef("corpus_notes"))),
                    new IsNull(Col("deleted_at", new AliasRef("s0"))),
                ]))),
            soft);
    }

    [Fact(DisplayName = "a soft-deletable entity starts every select with its live term and IncludingDeleted removes it")]
    public void Soft_entities_carry_the_live_term()
    {
        var live = Of<CorpusTask>().Where(t => t.Rank >= 2).BuildSelect();
        var all = Of<CorpusTask>().IncludingDeleted().BuildSelect();

        Assert.Equal<Node>(
            new Where([new IsNull(Col("deleted_at")), new Compare(CompareOp.GreaterOrEqual, Col("rank"), new Param(0, 2))]),
            live.Where!);
        Assert.Null(all.Where);
        Assert.Equal<Node>(new Star(), Assert.Single(all.Columns));
        Assert.Equal(new TableRef("corpus_tasks"), all.From);
    }

    [Fact(DisplayName = "order, paging and the row lock become their own nodes, a later OrderBy replaces the earlier terms and the key-preserving lock wins over the plain one")]
    public void Order_paging_and_locks_become_nodes()
    {
        var ordered = Of<CorpusNote>()
            .OrderBy(n => n.Title).ThenByDescending(n => n.Rank).Take(10).Skip(20).ForUpdate().ForNoKeyUpdate().BuildSelect();
        var replaced = Of<CorpusNote>().OrderBy(n => n.Title).OrderByDescending(n => n.Views).BuildSelect();

        Assert.Equal(
            new OrderBy([
                new OrderTerm(Col("title"), false, NullsPlacement.Last),
                new OrderTerm(Col("rank"), true, NullsPlacement.Last),
            ]),
            ordered.OrderBy);
        Assert.Equal(new Limit(10), ordered.Limit);
        Assert.Equal(new Offset(20), ordered.Offset);
        Assert.Equal(new RowLock(RowLockStrength.KeyPreserving), ordered.Lock);
        Assert.Equal(new OrderBy([new OrderTerm(Col("views"), true, NullsPlacement.Last)]), replaced.OrderBy);
        Assert.Null(replaced.Lock);
        Assert.Equal(new RowLock(RowLockStrength.KeyPreserving), Of<CorpusNote>().ForNoKeyUpdate().ForUpdate().BuildSelect().Lock);
    }

    [Fact(DisplayName = "redaction replaces one column of the select list with NULL or with a CASE over the predicate, and keeps the others as plain columns")]
    public void Redaction_becomes_select_list_nodes()
    {
        var redacted = Of<CorpusNote>().Redact(n => n.Body).BuildSelect();
        var conditional = Of<CorpusNote>().RedactWhen(n => n.Body, n => n.Active).BuildSelect();

        Assert.Equal(new Aliased(new NullLit(), "body"), redacted.Columns.Single(c => c is Aliased));
        Assert.Equal(Col("id"), redacted.Columns[0]);
        Assert.Equal(
            new Aliased(
                new CaseWhen(new Compare(CompareOp.Equal, Col("active"), new BoolLit(true)), new NullLit(), Col("body")),
                "body"),
            conditional.Columns.Single(c => c is Aliased));
    }

    [Fact(DisplayName = "the aggregate terms become aggregate nodes in the order they were added, and the FILTER aggregate wraps its predicate in a group")]
    public void Aggregates_become_nodes()
    {
        var tree = Of<CorpusNote>().Where(n => n.Active)
            .AggregateCount().AggregateCountDistinct(n => n.OwnerId).AggregateSum(n => n.Rank).AggregateMin(n => n.Rank)
            .AggregateCountWhere(n => n.Title == "a").BuildAggregates();

        Assert.Equal<Node>(
            [
                new Call(Fn.Count, [new Star()]),
                new Aggregate(AggregateFn.CountDistinct, Col("owner_id")),
                new Aggregate(AggregateFn.SumOrZero, Col("rank")),
                new Aggregate(AggregateFn.Min, Col("rank")),
                new CountFiltered(new Group(new Compare(CompareOp.Equal, Col("title"), new Param(0, "a")))),
            ],
            tree.Columns);
    }

    [Fact(DisplayName = "parameters are numbered in the order the builder was called, not the order the text mentions them, and a subquery continues the outer numbering")]
    public void Parameter_numbering_follows_the_builder_calls()
    {
        var query = Of<CorpusNote>()
            .OrderByPredicate(n => n.Title == "first")
            .Where(n => n.Title == "second")
            .WhereIn<CorpusTag>(n => n.Id, sub => sub.Where(t => t.Label == "inner"), t => t.NoteId)
            .Where(n => n.Rank > 3);

        var statement = query.CompileSelect();

        Assert.Equal(
            "SELECT * FROM \"corpus_notes\" WHERE \"title\" = @p1 AND \"id\" IN (SELECT \"note_id\" FROM \"corpus_tags\" WHERE \"label\" = @p2) AND \"rank\" > @p3 "
            + "ORDER BY (\"title\" = @p0) ASC NULLS LAST",
            statement.Text);
        Assert.Equal(new object?[] { "first", "second", "inner", 3 }, statement.Values);
        Assert.Equal(new[] { 0, 1, 2, 3 }, statement.Parameters.Select(p => p.Ordinal));
        Assert.Equal(statement.Values, query.BoundParameters);
    }

    [Fact(DisplayName = "the compiled statement and the rendered text agree, and every compiled parameter is the bound value of its ordinal")]
    public void Compile_and_render_agree()
    {
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var queries = new[]
        {
            Of<CorpusNote>().Where(n => n.Rank > 1).OrderBy(n => n.Title).Take(5),
            Of<CorpusNote>().Where(n => ids.Contains(n.Id) && n.Title.Contains("x")),
            Of<CorpusNote>().WhereExists<CorpusTag>((n, t) => t.NoteId == n.Id && t.Position > 3).Skip(2),
            Of<CorpusNote>().RedactWhen(n => n.Body, n => n.Title == "r").Where(n => n.Title == "f"),
        };

        foreach (var query in queries)
        {
            var compiled = query.CompileSelect();
            Assert.Equal(query.RenderSelect(), compiled.Text);
            foreach (var parameter in compiled.Parameters)
                Assert.Equal(query.BoundParameters[parameter.Ordinal], parameter.Value);
        }
    }

    [Fact(DisplayName = "the JSON markers become JSON nodes: a typed key comparison, a key test, the moved-field forms and the ordering terms")]
    public void Json_markers_become_nodes()
    {
        var legacy = new MovedFieldKey("legacy_name");

        Assert.Equal<Node>(
            new Compare(
                CompareOp.Equal,
                new Comparable(new JsonKeyText(Col("payload"), new Param(0, "count")), JsonValueOrder.Number),
                new Comparable(new Param(1, "10"), JsonValueOrder.Number)),
            Of<CorpusNote>().Where(n => Db.JsonKeyEquals(n.Payload, "count", "10", JsonValueOrder.Number)).WhereTerms.Single());
        Assert.Equal<Node>(new JsonKeyExists(Col("payload"), new Param(0, "name")), Of<CorpusNote>().Where(n => Db.JsonHasKey(n.Payload, "name")).WhereTerms.Single());
        Assert.Equal<Node>(new IsNull(new JsonKeyText(Col("meta"), new StringLit("legacy_name"))), Of<CorpusNote>().Where(n => Db.MovedFieldIsNull(n.Meta, legacy)).WhereTerms.Single());
        Assert.Equal<Node>(
            new Compare(CompareOp.Equal, new JsonKeyText(Col("meta"), new StringLit("legacy_name")), new Cast(new Param(0, "x"), CastType.Text)),
            Of<CorpusNote>().Where(n => Db.MovedFieldEquals(n.Meta, legacy, "x")).WhereTerms.Single());
        Assert.Equal<Node>(
            new ILike(new JsonKeyText(Col("meta"), new StringLit("legacy_name")), new Param(0, "%a~_b~%%"), '~'),
            Of<CorpusNote>().Where(n => Db.MovedFieldContains(n.Meta, legacy, "a_b%")).WhereTerms.Single());
        Assert.Equal(
            new OrderBy([new OrderTerm(new JsonKeyText(Col("payload"), new Param(0, "name")), true, NullsPlacement.First)]),
            Of<CorpusNote>().OrderByJsonKey(n => n.Payload, new JsonKey("name"), true, true).BuildSelect().OrderBy);
        Assert.Equal(
            new OrderBy([new OrderTerm(new MovedNumberOrEpochMillis(Col("meta"), new Param(0, "legacy_name"), Col("created_at")), false)]),
            Of<CorpusNote>().OrderByMovedFieldNumberOrEpochMillis(n => n.Meta, legacy, n => n.CreatedAt).BuildSelect().OrderBy);
    }

    [Fact(DisplayName = "ordering by a key of another table becomes a scalar subquery over a rank when the ordering is arranged, and over a typed value when it is not")]
    public void Cross_table_json_ordering_becomes_a_scalar_subquery()
    {
        var plain = Of<CorpusNote>().OrderByJsonKeyOf<CorpusTag>(
            (n, t) => t.NoteId == n.Id, t => t.Doc, new JsonKeyOrdering(new JsonKey("rank"), true, false, JsonValueOrder.Number, [])).BuildSelect().OrderBy!;
        var arranged = Of<CorpusNote>().OrderByJsonKeyOf<CorpusTag>(
            (n, t) => t.NoteId == n.Id, t => t.Doc, new JsonKeyOrdering(new JsonKey("state"), false, false, JsonValueOrder.Text, ["open", "done"])).BuildSelect().OrderBy!;

        var correlation = new Compare(CompareOp.Equal, Col("note_id", new AliasRef("s0")), Col("id", new TableRef("corpus_notes")));
        Node Wrap(Node extracted) => new ScalarSubquery(new Select([extracted], new TableRef("corpus_tags", "s0"), new Where([correlation])));
        var key = new JsonKeyText(Col("doc", new AliasRef("s0")), new Param(0, "rank"));

        Assert.Equal(new OrderTerm(Wrap(new Comparable(key, JsonValueOrder.Number)), true, NullsPlacement.Last), Assert.Single(plain.Terms));
        Assert.Equal(
            new OrderTerm(
                Wrap(new OptionRank([new Param(1, "open"), new Param(2, "done")], new JsonKeyText(Col("doc", new AliasRef("s0")), new Param(0, "state")))),
                false,
                NullsPlacement.Last),
            Assert.Single(arranged.Terms));
    }

    [Fact(DisplayName = "the provider capability check runs at compile: a dialect that allows a marker still gets a named refusal from a provider that lacks the capability, and no statement reaches the data source")]
    public async Task The_provider_capability_is_checked_at_compile()
    {
        var ids = new List<Guid>();
        var query = Of<CorpusNote>(new StubProvider(CapabilitySet.Nothing())).Where(n => Db.AnyOf(n.Id, ids));

        var refusal = Assert.Throws<CapabilityNotSupportedException>(() => query.CompileSelect());
        var asyncRefusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => query.ToListAsync());

        Assert.Equal(Capability.ArrayParameter, refusal.Capability);
        Assert.Equal("corpus_notes", refusal.Table);
        Assert.Equal("stub", asyncRefusal.Provider);
    }

    [Fact(DisplayName = "a context cannot be built without a provider")]
    public void A_context_needs_a_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new OrmContext(new UnreachableDataSource(), null!));
        Assert.Same(PostgresProvider.Instance, Context().Provider);
    }

    private sealed class StubProvider(CapabilitySet capabilities) : ISqlProvider
    {
        public string Id => "stub";

        public CapabilitySet Capabilities { get; } = capabilities;

        public ISerializer CreateSerializer() => new PostgresSerializer();
    }
}
