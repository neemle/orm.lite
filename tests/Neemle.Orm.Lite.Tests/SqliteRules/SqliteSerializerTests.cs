using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteSerializerTests
{
    private static Statement Compile(Node tree, StatementKind kind = StatementKind.Read) =>
        StatementCompiler.Compile(tree, SqliteProvider.Instance, kind);

    private static Select Over(params Node[] columns) => new(NodeList.Of(columns));

    private static Select From(Node? where = null, Limit? limit = null, Offset? offset = null, RowLock? rowLock = null) =>
        new([new Star()], new TableRef("t"), where is null ? null : new Where([where]), Limit: limit, Offset: offset, Lock: rowLock);

    [Fact(DisplayName = "a value list is lowered to an IN list with one parameter per element, named after the list and its position")]
    public void Any_of_is_lowered_to_an_in_list()
    {
        var statement = Compile(From(new AnyOf(new ColumnRef("id"), new Param(0, new[] { 10, 20, 30 }))));

        Assert.Equal("SELECT * FROM \"t\" WHERE \"id\" IN (@p0_0, @p0_1, @p0_2)", statement.Text);
        Assert.Equal(["p0_0", "p0_1", "p0_2"], statement.Parameters.Select(p => p.Name));
        Assert.Equal([10, 20, 30], statement.Parameters.Select(p => p.Value));
    }

    [Fact(DisplayName = "an empty list is an empty IN list, which matches nothing, and a list beside another parameter keeps both")]
    public void An_empty_list_matches_nothing()
    {
        var empty = Compile(From(new AnyOf(new ColumnRef("id"), new Param(0, Array.Empty<Guid>()))));
        var beside = Compile(From(new And(
            new AnyOf(new ColumnRef("id"), new Param(0, new[] { 1, 2 })),
            new Compare(CompareOp.Equal, new ColumnRef("n"), new Param(1, 5)))));

        Assert.Equal("SELECT * FROM \"t\" WHERE \"id\" IN ()", empty.Text);
        Assert.Empty(empty.Parameters);
        Assert.Equal("SELECT * FROM \"t\" WHERE (\"id\" IN (@p0_0, @p0_1) AND \"n\" = @p1)", beside.Text);
        Assert.Equal(["p0_0", "p0_1", "p1"], beside.Parameters.Select(p => p.Name));
    }

    [Fact(DisplayName = "a list is accepted up to the bind limit and refused above it, by name, before any statement runs")]
    public void The_bind_limit_is_exact()
    {
        var atLimit = Compile(From(new AnyOf(new ColumnRef("id"), new Param(0, new int[SqliteProvider.BindParameterLimit]))));
        var tooMany = Assert.Throws<NotSupportedException>(() =>
            Compile(From(new AnyOf(new ColumnRef("id"), new Param(0, new int[SqliteProvider.BindParameterLimit + 1])))));

        Assert.Equal(SqliteProvider.BindParameterLimit, atLimit.Parameters.Count);
        Assert.Contains(SqliteProvider.BindParameterLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), tooMany.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an array parameter that is not a bound collection is refused")]
    public void A_non_collection_array_is_refused()
    {
        Assert.Throws<NotSupportedException>(() => Compile(From(new AnyOf(new ColumnRef("id"), new Param(0, "text")))));
        Assert.Throws<NotSupportedException>(() => Compile(From(new AnyOf(new ColumnRef("id"), new ColumnRef("other")))));
    }

    [Fact(DisplayName = "an offset without a limit gets the limit SQLite requires, and a limit with an offset is untouched")]
    public void Paging_is_valid_for_sqlite()
    {
        Assert.Equal("SELECT * FROM \"t\" LIMIT -1 OFFSET 5", Compile(From(offset: new Offset(5))).Text);
        Assert.Equal("SELECT * FROM \"t\" LIMIT 3 OFFSET 5", Compile(From(limit: new Limit(3), offset: new Offset(5))).Text);
        Assert.Equal("SELECT * FROM \"t\" LIMIT 3", Compile(From(limit: new Limit(3))).Text);
    }

    [Fact(DisplayName = "the clock is a fixed-width UTC text with seven fractional digits, so it sorts against what the codec writes")]
    public void The_clock_is_fixed_width()
    {
        Assert.Equal("SELECT (strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z')", Compile(Over(new ClockNow())).Text);
        Assert.Equal(
            "SELECT (strftime('%Y-%m-%dT%H:%M:%f', unixepoch('subsec') + @p0 * 60, 'unixepoch') || '0000Z')",
            Compile(Over(new ClockPlusMinutes(new Param(0, 5)))).Text);
    }

    [Fact(DisplayName = "a row lock writes nothing, the unit of work's write transaction being the lock, and a named lock evaluates to a constant")]
    public void Locks_are_emulated_by_the_transaction()
    {
        Assert.Equal("SELECT * FROM \"t\"", Compile(From(rowLock: new RowLock(RowLockStrength.Update))).Text);
        Assert.Equal("SELECT * FROM \"t\"", Compile(From(rowLock: new RowLock(RowLockStrength.KeyPreserving))).Text);
        Assert.Equal("SELECT 1", Compile(Over(new NamedLock(NamedLockKind.Exclusive, new Param(0, "k")))).Text);
        Assert.Equal("SELECT 1", Compile(Over(new NamedLock(NamedLockKind.Shared, new Param(0, "k")))).Text);
    }

    [Fact(DisplayName = "an ignore-case match is the registered case-folding function, and a case-sensitive one stays LIKE")]
    public void Ignore_case_uses_the_registered_function()
    {
        Assert.Equal(
            "SELECT orm_ilike(\"a\", @p0, '\\')",
            Compile(Over(new ILike(new ColumnRef("a"), new Param(0, "%x%"), '\\'))).Text);
        Assert.Equal(
            "SELECT \"a\" LIKE @p0 ESCAPE '\\'",
            Compile(Over(new Like(new ColumnRef("a"), new Param(0, "%x%"), '\\'))).Text);
    }

    [Fact(DisplayName = "casts use the type names SQLite knows, a cast to a JSON document validates and normalizes through the registered function, and a boolean or decimal parameter cast to text binds its text")]
    public void Casts_use_sqlite_type_names()
    {
        Assert.Equal("SELECT CAST(@p0 AS text)", Compile(Over(new Cast(new Param(0, 1), CastType.Text))).Text);
        Assert.Equal("SELECT CAST(@p0 AS integer)", Compile(Over(new Cast(new Param(0, 1), CastType.Boolean))).Text);
        Assert.Equal("SELECT CAST(@p0 AS real)", Compile(Over(new Cast(new Param(0, 1), CastType.Double))).Text);
        Assert.Contains("no exact decimal cast", Assert.Throws<NotSupportedException>(() => Compile(Over(new Cast(new Param(0, 1), CastType.Decimal)))).Message, StringComparison.Ordinal);
        Assert.Equal("SELECT orm_json(@p0)", Compile(Over(new Cast(new Param(0, "{}"), CastType.Json))).Text);
        var flag = Compile(Over(new Cast(new Param(0, true), CastType.Text)));
        var number = Compile(Over(new Cast(new Param(0, 12.50m), CastType.Text)));
        Assert.Equal("SELECT CAST(@p0 AS text)", flag.Text);
        Assert.Equal(["true"], flag.Parameters.Select(p => p.Value));
        Assert.Equal(["12.50"], number.Parameters.Select(p => p.Value));
    }

    [Fact(DisplayName = "null-safe equality, the filtered count, null ordering, upsert and returning keep the standard text SQLite reads")]
    public void Standard_text_is_kept()
    {
        Assert.Equal("SELECT \"a\" IS NOT DISTINCT FROM @p0", Compile(Over(new NullSafeEquals(new ColumnRef("a"), new Param(0, 1)))).Text);
        Assert.Equal("SELECT count(*) FILTER (WHERE \"a\" IS NULL)", Compile(Over(new CountFiltered(new IsNull(new ColumnRef("a"))))).Text);
        Assert.Equal(
            "INSERT INTO \"t\" (\"id\") VALUES (@p0) ON CONFLICT (\"id\") DO UPDATE SET \"n\" = @p1 RETURNING \"id\"",
            Compile(
                new Insert(
                    new TableRef("t"),
                    [new ColumnRef("id")],
                    [new ValuesRow([new Param(0, 1)])],
                    new Conflict([new ColumnRef("id")], null, new ConflictUpdate([new Assignment(new ColumnRef("n"), new Param(1, 2))])),
                    new Returning([new ColumnRef("id")])),
                StatementKind.Write).Text);
    }

    [Fact(DisplayName = "every JSON node is lowered to a registered function or a standard construct, one spelling per node, with the key and value bound and never spliced")]
    public void Json_nodes_are_lowered()
    {
        var d = new ColumnRef("d");
        (Node Node, string Text)[] cases =
        [
            (new JsonKeyText(d, new Param(0, "k")), "SELECT orm_json_text(\"d\", @p0)"),
            (new JsonKeyExists(d, new Param(0, "k")), "SELECT orm_json_has(\"d\", @p0)"),
            (new JsonArrayContainsString(d, new Param(0, "k"), new Param(1, "v")), "SELECT orm_json_array_has(\"d\", @p0, @p1)"),
            (new JsonArrayContainsAny(d, new Param(0, "k"), [new Param(1, "v"), new Param(2, "w")]),
                "SELECT (orm_json_array_has(\"d\", @p0, @p1) OR orm_json_array_has(\"d\", @p0, @p2))"),
            (new Comparable(new JsonKeyText(d, new Param(0, "k")), JsonValueOrder.Text), "SELECT orm_json_text(\"d\", @p0)"),
            (new Comparable(new JsonKeyText(d, new Param(0, "k")), JsonValueOrder.Number), "SELECT orm_json_number(orm_json_text(\"d\", @p0))"),
            (new Comparable(new JsonKeyText(d, new Param(0, "k")), JsonValueOrder.Boolean), "SELECT orm_json_boolean(orm_json_text(\"d\", @p0))"),
            (new OptionRank([new Param(0, "a"), new Param(1, "b")], new Param(2, "n")), "SELECT CASE @p2 WHEN @p0 THEN 1 WHEN @p1 THEN 2 END"),
            (new MovedNumberOrEpochMillis(d, new Param(0, "k"), new ColumnRef("ts")),
                "SELECT COALESCE(orm_json_double(orm_json_text(\"d\", @p0)), orm_epoch_millis(\"ts\"))"),
            (new JsonDocumentOrEmpty(d), "SELECT COALESCE(\"d\", '{}')"),
            (new JsonConcat(d, new Param(0, "{}")), "SELECT orm_json_concat(\"d\", @p0)"),
            (new JsonRemoveKey(d, new Param(0, "k")), "SELECT orm_json_remove(\"d\", @p0)"),
            (new JsonObjectOf(new Param(0, "k"), new Param(1, 5)), "SELECT orm_json_object(@p0, @p1)"),
            (new JsonIntOrZero(d), "SELECT orm_json_int(\"d\")"),
        ];

        foreach (var (node, text) in cases)
        {
            Assert.Equal(text, Compile(Over(node)).Text);
            Assert.DoesNotContain(node.Requires(), c => SqliteProvider.Instance.Capabilities.Of(c) == Support.None);
        }
    }

    [Fact(DisplayName = "the serializer refuses an order it has no rule for and an arrangement with no options, so a new enum member or a degenerate tree is not half-written")]
    public void The_serializer_refuses_what_it_cannot_spell()
    {
        var serializer = new SqliteSerializer();

        Assert.Throws<NotSupportedException>(() => serializer.Serialize(new Comparable(new ColumnRef("d"), (JsonValueOrder)99)));
        Assert.Throws<ArgumentException>(() => serializer.Serialize(new OptionRank([], new ColumnRef("d"))));
    }

    [Fact(DisplayName = "an unsupported session lock is refused by capability before a connection is needed")]
    public void A_session_lock_is_refused()
    {
        var refusal = Assert.Throws<CapabilityNotSupportedException>(() => Compile(AdvisorySessionLocks.TryLock(1)));

        Assert.Equal(Capability.SessionLock, refusal.Capability);
    }
}
