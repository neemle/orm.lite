using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class PostgresRulesTests
{
    private static readonly ColumnRef A = new("a");

    private static readonly ColumnRef Doc = new("d");

    private static Statement Compile(Node tree) =>
        StatementCompiler.Compile(tree, PostgresProvider.Instance, StatementKind.Read);

    private static string Text(Node tree) => Compile(tree).Text;

    private static Select From(params Node[] columns) => new(NodeList.Of(columns), new TableRef("t"));

    [Fact(DisplayName = "a select renders its clauses in the fixed order, joins the where terms flat with AND and keeps each And/Or grouped")]
    public void A_select_renders_its_clauses_in_order()
    {
        var tree = new Select(
            [new ColumnRef("id"), new Aliased(new NullLit(), "body")],
            new TableRef("notes"),
            new Where([
                new IsNull(new ColumnRef("deleted_at")),
                new And(
                    new Compare(CompareOp.Greater, new ColumnRef("rank"), new Param(0, 1)),
                    new InList(new ColumnRef("id"), [new Param(1, "a"), new Param(2, "b")])),
            ]),
            new OrderBy([new OrderTerm(new ColumnRef("title"), false, NullsPlacement.Last)]),
            new Limit(10),
            new Offset(20),
            new RowLock(RowLockStrength.Update));

        var statement = Compile(tree);

        Assert.Equal(
            "SELECT \"id\", NULL AS \"body\" FROM \"notes\" WHERE \"deleted_at\" IS NULL AND (\"rank\" > @p0 AND \"id\" IN (@p1, @p2)) "
            + "ORDER BY \"title\" ASC NULLS LAST LIMIT 10 OFFSET 20 FOR UPDATE",
            statement.Text);
        Assert.Equal(StatementKind.Read, statement.Kind);
        Assert.Equal(new object?[] { 1, "a", "b" }, statement.Values);
        Assert.Equal(new[] { "p0", "p1", "p2" }, statement.Parameters.Select(p => p.Name));
    }

    [Theory(DisplayName = "each comparison operator renders its own spelling between the operands")]
    [InlineData(CompareOp.Equal, "=")]
    [InlineData(CompareOp.NotEqual, "<>")]
    [InlineData(CompareOp.Less, "<")]
    [InlineData(CompareOp.LessOrEqual, "<=")]
    [InlineData(CompareOp.Greater, ">")]
    [InlineData(CompareOp.GreaterOrEqual, ">=")]
    public void A_comparison_renders_its_operator(CompareOp op, string spelling) =>
        Assert.Equal("\"a\" " + spelling + " @p0", Text(new Compare(op, A, new Param(0, 1))));

    [Fact(DisplayName = "And and Or group their pair, Not wraps its operand, Group parenthesises, and the null tests read as written")]
    public void Logic_renders_with_its_grouping()
    {
        var left = new Compare(CompareOp.Equal, A, new Param(0, 1));
        var right = new IsNull(new ColumnRef("b"));

        Assert.Equal("(\"a\" = @p0 AND \"b\" IS NULL)", Text(new And(left, right)));
        Assert.Equal("(\"a\" = @p0 OR \"b\" IS NULL)", Text(new Or(left, right)));
        Assert.Equal("NOT (\"a\" = @p0)", Text(new Not(left)));
        Assert.Equal("(\"a\" = @p0)", Text(new Group(left)));
        Assert.Equal("\"a\" IS NOT NULL", Text(new IsNotNull(A)));
        Assert.Equal("(((\"a\" = @p0 AND \"b\" IS NULL) OR \"a\" IS NOT NULL))", Text(new Group(new Or(new And(left, right), new IsNotNull(A)))));
    }

    [Fact(DisplayName = "a list membership renders its items comma separated, and a subquery membership renders the whole select in parentheses")]
    public void Membership_renders()
    {
        Assert.Equal("\"a\" IN (@p0, @p1, @p2)", Text(new InList(A, [new Param(0, 1), new Param(1, 2), new Param(2, 3)])));
        Assert.Equal(
            "\"a\" IN (SELECT \"note_id\" FROM \"tags\" WHERE \"position\" > @p0)",
            Text(new InSubquery(A, new Select([new ColumnRef("note_id")], new TableRef("tags"),
                new Where([new Compare(CompareOp.Greater, new ColumnRef("position"), new Param(0, 1))])))));
    }

    [Fact(DisplayName = "LIKE and ILIKE carry the escape character as a quoted character, and a quote in it is doubled")]
    public void Like_renders_its_escape()
    {
        Assert.Equal("\"a\" LIKE @p0 ESCAPE '~'", Text(new Like(A, new Param(0, "%x%"), '~')));
        Assert.Equal("\"a\" ILIKE @p0 ESCAPE '~'", Text(new ILike(A, new Param(0, "%x%"), '~')));
        Assert.Equal("\"a\" LIKE @p0 ESCAPE ''''", Text(new Like(A, new Param(0, "%x%"), '\'')));
    }

    [Fact(DisplayName = "a correlated EXISTS qualifies its columns by alias and by table, and a scalar subquery renders inside an order term")]
    public void Subqueries_render_with_their_qualifiers()
    {
        var inner = new Select(
            [new IntLit(1)],
            new TableRef("tags", "s0"),
            new Where([
                new Compare(CompareOp.Equal, new ColumnRef("note_id", new AliasRef("s0")), new ColumnRef("id", new TableRef("notes"))),
                new IsNull(new ColumnRef("deleted_at", new AliasRef("s0"))),
            ]));

        Assert.Equal(
            "EXISTS (SELECT 1 FROM \"tags\" s0 WHERE s0.\"note_id\" = \"notes\".\"id\" AND s0.\"deleted_at\" IS NULL)",
            Text(new Exists(inner)));
        Assert.Equal(
            "(SELECT 1 FROM \"tags\" s0 WHERE s0.\"note_id\" = \"notes\".\"id\" AND s0.\"deleted_at\" IS NULL) DESC NULLS FIRST",
            Text(new OrderTerm(new ScalarSubquery(inner), true, NullsPlacement.First)));
    }

    [Fact(DisplayName = "function calls and aggregates attach their parenthesis to the name, and the keyword forms keep the spelling the corpus pins")]
    public void Calls_and_aggregates_render()
    {
        Assert.Equal("count(*)", Text(new Call(Fn.Count, [new Star()])));
        Assert.Equal("lower(\"a\")", Text(new Call(Fn.Lower, [A])));
        Assert.Equal("coalesce(sum(\"a\"), 0)", Text(new Call(Fn.Coalesce, [new Call(Fn.Sum, [A]), new IntLit(0)])));
        Assert.Equal("count(DISTINCT \"a\")", Text(new Aggregate(AggregateFn.CountDistinct, A)));
        Assert.Equal("COALESCE(SUM(COALESCE(\"a\", 0)), 0)", Text(new Aggregate(AggregateFn.SumOrZero, A)));
        Assert.Equal("min(\"a\")", Text(new Aggregate(AggregateFn.Min, A)));
        Assert.Equal(
            "count(*) FILTER (WHERE (\"a\" = true))",
            Text(new CountFiltered(new Group(new Compare(CompareOp.Equal, A, new BoolLit(true))))));
    }

    [Fact(DisplayName = "a cast to text is spelled as a function, any other cast as a postfix operator, and a case expression renders with and without its else")]
    public void Casts_and_case_render()
    {
        Assert.Equal("CAST(@p0 AS text)", Text(new Cast(new Param(0, 1), CastType.Text)));
        Assert.Equal("@p0::jsonb", Text(new Cast(new Param(0, "{}"), CastType.Json)));
        Assert.Equal("@p0::numeric", Text(new Cast(new Param(0, 1m), CastType.Decimal)));
        Assert.Equal("@p0::boolean", Text(new Cast(new Param(0, true), CastType.Boolean)));
        Assert.Equal("@p0::double precision", Text(new Cast(new Param(0, 1.5), CastType.Double)));
        Assert.Equal("CASE WHEN \"a\" = true THEN NULL ELSE \"b\" END",
            Text(new CaseWhen(new Compare(CompareOp.Equal, A, new BoolLit(true)), new NullLit(), new ColumnRef("b"))));
        Assert.Equal("CASE WHEN \"a\" IS NULL THEN 1 END", Text(new CaseWhen(new IsNull(A), new IntLit(1))));
    }

    [Fact(DisplayName = "an order term always names its direction and names the null placement only when it was asked for")]
    public void Order_terms_render()
    {
        Assert.Equal("\"a\" ASC", Text(new OrderTerm(A, false)));
        Assert.Equal("\"a\" DESC", Text(new OrderTerm(A, true)));
        Assert.Equal("\"a\" DESC NULLS FIRST", Text(new OrderTerm(A, true, NullsPlacement.First)));
        Assert.Equal("\"a\" ASC NULLS LAST", Text(new OrderTerm(A, false, NullsPlacement.Last)));
        Assert.Equal("ORDER BY \"a\" ASC, \"b\" DESC", Text(new OrderBy([new OrderTerm(A, false), new OrderTerm(new ColumnRef("b"), true)])));
    }

    [Fact(DisplayName = "paging and locks render after the order, each lock strength with its own words, and either paging clause stands alone")]
    public void Paging_and_locks_render()
    {
        Assert.Equal("SELECT * FROM \"t\" LIMIT 5", Text(new Select([new Star()], new TableRef("t"), Limit: new Limit(5))));
        Assert.Equal("SELECT * FROM \"t\" OFFSET 7", Text(new Select([new Star()], new TableRef("t"), Offset: new Offset(7))));
        Assert.Equal("SELECT * FROM \"t\" LIMIT 0 OFFSET 0", Text(new Select([new Star()], new TableRef("t"), Limit: new Limit(0), Offset: new Offset(0))));
        Assert.Equal("SELECT * FROM \"t\" FOR UPDATE", Text(new Select([new Star()], new TableRef("t"), Lock: new RowLock(RowLockStrength.Update))));
        Assert.Equal("SELECT * FROM \"t\" LIMIT 1 FOR NO KEY UPDATE",
            Text(new Select([new Star()], new TableRef("t"), Limit: new Limit(1), Lock: new RowLock(RowLockStrength.KeyPreserving))));
    }

    [Fact(DisplayName = "literals render as written: the clock as now(), booleans in lower case, text quote-doubled, identifiers quote-doubled, integers signed")]
    public void Literals_render()
    {
        Assert.Equal("now()", Text(new ClockNow()));
        Assert.Equal("true", Text(new BoolLit(true)));
        Assert.Equal("false", Text(new BoolLit(false)));
        Assert.Equal("'it''s'", Text(new StringLit("it's")));
        Assert.Equal("''", Text(new StringLit("")));
        Assert.Equal("\"a\"\"b\"", Text(new ColumnRef("a\"b")));
        Assert.Equal("-5", Text(new IntLit(-5)));
        Assert.Equal("\"t\".\"c\"", Text(new ColumnRef("c", new TableRef("t"))));
        Assert.Equal("s0.\"c\"", Text(new ColumnRef("c", new AliasRef("s0"))));
        Assert.Equal("\"a\" AS \"x\"", Text(new Aliased(A, "x")));
    }

    [Fact(DisplayName = "the array parameter and the null-safe equality render their Postgres forms")]
    public void Provider_realised_comparisons_render()
    {
        Assert.Equal("\"a\" = ANY(@p0)", Text(new AnyOf(A, new Param(0, new[] { 1, 2 }))));
        Assert.Equal("\"a\" IS NOT DISTINCT FROM @p0", Text(new NullSafeEquals(A, new Param(0, null))));
    }

    [Fact(DisplayName = "a typed JSON comparison reads text as is, and reads a number or a boolean through a validity guard and a cast")]
    public void Comparable_renders_per_order()
    {
        var key = new JsonKeyText(Doc, new Param(0, "k"));

        Assert.Equal("(\"d\" ->> @p0)", Text(new Comparable(key, JsonValueOrder.Text)));
        Assert.Equal(
            "(CASE WHEN pg_input_is_valid((\"d\" ->> @p0), 'numeric') THEN (\"d\" ->> @p0) END)::numeric",
            Text(new Comparable(key, JsonValueOrder.Number)));
        Assert.Equal(
            "(CASE WHEN pg_input_is_valid((\"d\" ->> @p0), 'boolean') THEN (\"d\" ->> @p0) END)::boolean",
            Text(new Comparable(key, JsonValueOrder.Boolean)));
        Assert.Equal(
            "(CASE WHEN pg_input_is_valid(@p1, 'numeric') THEN @p1 END)::numeric",
            Text(new Comparable(new Param(1, "10"), JsonValueOrder.Number)));
        var refusal = Assert.Throws<NotSupportedException>(() => Text(new Comparable(key, (JsonValueOrder)9)));
        Assert.Contains("JsonValueOrder.9", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the JSON key tests render the existence function and the array-membership forms, with the extracted value written wherever it is used")]
    public void Json_key_tests_render()
    {
        Assert.Equal("jsonb_exists(\"d\", @p0)", Text(new JsonKeyExists(Doc, new Param(0, "k"))));
        Assert.Equal(
            "(jsonb_typeof((\"d\" -> @p0)) = 'array' AND jsonb_exists((\"d\" -> @p0), @p1))",
            Text(new JsonArrayContainsString(Doc, new Param(0, "k"), new Param(1, "v"))));
        Assert.Equal(
            "(jsonb_typeof((\"d\" -> @p0)) = 'array' AND ((\"d\" -> @p0) @> to_jsonb(CAST(@p1 AS text)) OR (\"d\" -> @p0) @> to_jsonb(CAST(@p2 AS text))))",
            Text(new JsonArrayContainsAny(Doc, new Param(0, "k"), [new Param(1, "x"), new Param(2, "y")])));
        Assert.Equal(
            "(jsonb_typeof((\"d\" -> @p0)) = 'array' AND ((\"d\" -> @p0) @> to_jsonb(CAST(@p1 AS text))))",
            Text(new JsonArrayContainsAny(Doc, new Param(0, "k"), [new Param(1, "x")])));
    }

    [Fact(DisplayName = "an option rank renders the array-position form and refuses an empty option list")]
    public void Option_rank_renders()
    {
        var key = new JsonKeyText(Doc, new Param(0, "k"));

        Assert.Equal(
            "array_position(ARRAY[@p1, @p2]::text[], (\"d\" ->> @p0))",
            Text(new OptionRank([new Param(1, "open"), new Param(2, "done")], key)));
        Assert.Throws<ArgumentException>(() => Text(new OptionRank([], key)));
    }

    [Fact(DisplayName = "the moved-field order term renders its number-or-epoch-milliseconds form with the key as a literal")]
    public void Moved_number_or_epoch_millis_renders()
    {
        Assert.Equal(
            "COALESCE((CASE WHEN pg_input_is_valid((\"m\" ->> 'k'), 'double precision') THEN (\"m\" ->> 'k') END)::double precision, "
            + "FLOOR(EXTRACT(EPOCH FROM \"ts\") * 1000)::double precision)",
            Text(new MovedNumberOrEpochMillis(new ColumnRef("m"), new StringLit("k"), new ColumnRef("ts"))));
    }

    [Fact(DisplayName = "the parameters of a statement are the distinct Param nodes in ordinal order, each named by its ordinal, whatever order the text mentions them in")]
    public void Parameters_follow_their_ordinals()
    {
        var shared = new Param(3, "s");
        var tree = new Select(
            [new Compare(CompareOp.Equal, A, new Param(5, "late")), new Compare(CompareOp.Equal, A, shared)],
            new TableRef("t"),
            new Where([new Compare(CompareOp.Equal, A, shared), new Compare(CompareOp.Equal, A, new Param(1, "early"))]));

        var statement = Compile(tree);

        Assert.Equal(
            "SELECT \"a\" = @p5, \"a\" = @p3 FROM \"t\" WHERE \"a\" = @p3 AND \"a\" = @p1",
            statement.Text);
        Assert.Equal(new[] { 1, 3, 5 }, statement.Parameters.Select(p => p.Ordinal));
        Assert.Equal(new[] { "p1", "p3", "p5" }, statement.Parameters.Select(p => p.Name));
        Assert.Equal(new object?[] { "early", "s", "late" }, statement.Values);
    }

    [Fact(DisplayName = "rendering is repeatable and does not share state between two compilations of the same tree")]
    public void Rendering_is_repeatable()
    {
        var tree = From(new Compare(CompareOp.Equal, A, new Param(0, 1)));

        Assert.Equal(Compile(tree).Values, Compile(tree).Values);
        Assert.Equal(Text(tree), Text(tree));
        Assert.Single(Compile(tree).Parameters);
    }
}
