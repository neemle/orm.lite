using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class WriteRulesTests
{
    private static readonly ColumnRef A = new("a");

    private static readonly ColumnRef Doc = new("d");

    private static readonly TableRef T = new("t");

    private static Statement Postgres(Node tree) =>
        StatementCompiler.Compile(tree, PostgresProvider.Instance, StatementKind.Write);

    private static Statement Standard(Node tree) =>
        StatementCompiler.Compile(tree, new Provider(CapabilitySet.Everything(), () => new FixtureSerializer()), StatementKind.Write);

    private static string Text(Node tree) => Postgres(tree).Text;

    [Fact(DisplayName = "an insert writes its column list and one tuple per row, comma separated, and the clock renders as the provider's now")]
    public void Insert_renders_its_tuples()
    {
        var tree = new Insert(
            T,
            [A, new ColumnRef("b"), new ColumnRef("c")],
            [new ValuesRow([new Param(0, 1), new Param(1, "x"), new ClockNow()]), new ValuesRow([new Param(2, 2), new Param(3, "y"), new ClockNow()])]);

        var statement = Postgres(tree);

        Assert.Equal(
            "INSERT INTO \"t\" (\"a\", \"b\", \"c\") VALUES (@p0, @p1, now()), (@p2, @p3, now())", statement.Text);
        Assert.Equal(new object?[] { 1, "x", 2, "y" }, statement.Values);
        Assert.Equal(StatementKind.Write, statement.Kind);
    }

    [Fact(DisplayName = "a conflict clause writes its target, the target predicate, then DO NOTHING or DO UPDATE SET with its own guard, in that order")]
    public void Conflict_renders_every_part()
    {
        var row = new ValuesRow([new Param(0, 1)]);
        var set = new Assignment(new ColumnRef("b"), new Param(1, 2));

        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES (@p0) ON CONFLICT DO NOTHING",
            Text(new Insert(T, [A], [row], new Conflict([]))));
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES (@p0) ON CONFLICT (\"a\", \"b\") WHERE \"d\" IS NULL DO NOTHING",
            Text(new Insert(T, [A], [row], new Conflict([A, new ColumnRef("b")], new IsNull(Doc)))));
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES (@p0) ON CONFLICT (\"a\") DO UPDATE SET \"b\" = @p1",
            Text(new Insert(T, [A], [row], new Conflict([A], null, new ConflictUpdate([set])))));
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES (@p0) ON CONFLICT (\"a\") WHERE \"d\" IS NULL DO UPDATE SET \"b\" = @p1, \"c\" = now() WHERE \"t\".\"d\" IS NULL RETURNING \"a\"",
            Text(new Insert(
                T, [A], [row],
                new Conflict(
                    [A],
                    new IsNull(Doc),
                    new ConflictUpdate([set, new Assignment(new ColumnRef("c"), new ClockNow())], new IsNull(new ColumnRef("d", T)))),
                new Returning([A]))));
    }

    [Fact(DisplayName = "an update writes SET, the where terms flat, and RETURNING last; a delete writes FROM; a bare delete has no where")]
    public void Update_and_delete_render()
    {
        var where = new Where([new Compare(CompareOp.Equal, A, new Param(0, 1)), new IsNull(Doc)]);

        Assert.Equal(
            "UPDATE \"t\" SET \"b\" = @p1, \"c\" = NULL WHERE \"a\" = @p0 AND \"d\" IS NULL RETURNING \"a\", \"b\"",
            Text(new Update(
                T,
                [new Assignment(new ColumnRef("b"), new Param(1, 2)), new Assignment(new ColumnRef("c"), new NullLit())],
                where,
                new Returning([A, new ColumnRef("b")]))));
        Assert.Equal("DELETE FROM \"t\" WHERE \"a\" = @p0 AND \"d\" IS NULL", Text(new Delete(T, where)));
        Assert.Equal("DELETE FROM \"t\"", Text(new Delete(T)));
    }

    [Fact(DisplayName = "addition and coalesce render as written, and the interval assignment adds make_interval to now")]
    public void Arithmetic_and_interval_render()
    {
        var set = new Assignment(A, new Plus(new Coalesce(A, new IntLit(0)), new Param(0, 3)));

        Assert.Equal("UPDATE \"t\" SET \"a\" = COALESCE(\"a\", 0) + @p0", Text(new Update(T, [set])));
        Assert.Equal(
            "UPDATE \"t\" SET \"a\" = COALESCE(@p0, \"a\")",
            Text(new Update(T, [new Assignment(A, new Coalesce(new Param(0, 1), A))])));
        Assert.Equal(
            "UPDATE \"t\" SET \"a\" = now() + make_interval(mins => @p0)",
            Text(new Update(T, [new Assignment(A, new ClockPlusMinutes(new Param(0, 15)))])));
        Assert.Equal(
            "UPDATE \"t\" SET \"a\" = \"t\".\"a\" + 3",
            Text(new Update(T, [new Assignment(A, new Plus(new ColumnRef("a", T), new IntLit(3)))])));
    }

    [Fact(DisplayName = "an exclusive and a shared named lock are two function names over the same hashed key, and the key keeps the name it was given")]
    public void Named_locks_render()
    {
        var key = new Param(0, "key-1", "k");

        var exclusive = Postgres(new Select([new NamedLock(NamedLockKind.Exclusive, key)]));
        var shared = Postgres(new Select([new NamedLock(NamedLockKind.Shared, key)]));

        Assert.Equal("SELECT pg_advisory_xact_lock(hashtextextended(@k, 0))", exclusive.Text);
        Assert.Equal("SELECT pg_advisory_xact_lock_shared(hashtextextended(@k, 0))", shared.Text);
        Assert.Equal(new[] { "k" }, exclusive.Parameters.Select(p => p.Name));
        Assert.Equal(new object?[] { "key-1" }, exclusive.Values);
    }

    [Fact(DisplayName = "a select without a table writes no FROM, and the same parameter node written twice is one parameter")]
    public void A_select_may_have_no_table()
    {
        var statement = Postgres(new Select([new Param(0, 1), new Param(0, 1)]));

        Assert.Equal("SELECT @p0, @p0", statement.Text);
        Assert.Single(statement.Parameters);
    }

    [Fact(DisplayName = "the JSON document nodes render the empty-document default, the concatenation, the key removal, the object builder and the integer reading")]
    public void Json_write_nodes_render()
    {
        var empty = new JsonDocumentOrEmpty(Doc);

        Assert.Equal("SELECT COALESCE(\"d\", '{}'::jsonb)", Text(new Select([empty])));
        Assert.Equal("SELECT COALESCE(\"d\", '{}'::jsonb) || @p0::jsonb", Text(new Select([new JsonConcat(empty, new Cast(new Param(0, Jsonb.Parse("{}")), CastType.Json))])));
        Assert.Equal(
            "SELECT ((COALESCE(\"d\", '{}'::jsonb) - @p0::text) - @p1::text)",
            Text(new Select([new JsonRemoveKey(new JsonRemoveKey(empty, new Param(0, "x")), new Param(1, "y"))])));
        Assert.Equal(
            "SELECT jsonb_build_object(@p0::text, COALESCE(NULLIF((\"d\" ->> @p0), '')::int, 0) + @p1)",
            Text(new Select([new JsonObjectOf(
                new Param(0, "k"),
                new Plus(new JsonIntOrZero(new JsonKeyText(Doc, new Param(0, "k"))), new Param(1, 2)))])));
    }

    [Fact(DisplayName = "the standard grammar writes the statements every provider shares, with the standard clock, and refuses what only a provider can spell")]
    public void The_standard_grammar_writes_shared_statements()
    {
        var where = new Where([new Compare(CompareOp.Equal, A, new Param(0, 1))]);

        Assert.Equal(
            "UPDATE \"t\" SET \"deleted_at\" = CURRENT_TIMESTAMP WHERE \"a\" = @p0",
            Standard(new Update(T, [new Assignment(new ColumnRef("deleted_at"), new ClockNow())], where)).Text);
        Assert.Equal("DELETE FROM \"t\" WHERE \"a\" = @p0", Standard(new Delete(T, where)).Text);
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES (@p0) ON CONFLICT (\"a\") DO NOTHING",
            Standard(new Insert(T, [A], [new ValuesRow([new Param(0, 1)])], new Conflict([A]))).Text);
        Assert.Throws<NotSupportedException>(() => Standard(new Select([new ClockPlusMinutes(new Param(0, 1))])));
        Assert.Throws<NotSupportedException>(() => Standard(new Select([new JsonConcat(A, A)])));
    }

    [Theory(DisplayName = "a provider without the capability a write node declares refuses it by name before any text is written")]
    [InlineData(typeof(Conflict), Capability.Upsert)]
    [InlineData(typeof(Returning), Capability.Returning)]
    [InlineData(typeof(ClockPlusMinutes), Capability.IntervalArithmetic)]
    [InlineData(typeof(NamedLock), Capability.NamedLock)]
    [InlineData(typeof(JsonDocumentOrEmpty), Capability.JsonDocument)]
    [InlineData(typeof(JsonConcat), Capability.JsonDocument)]
    [InlineData(typeof(JsonRemoveKey), Capability.JsonDocument)]
    [InlineData(typeof(JsonObjectOf), Capability.JsonDocument)]
    [InlineData(typeof(JsonIntOrZero), Capability.JsonDocument)]
    public void A_missing_capability_refuses_a_write_node(Type node, Capability capability)
    {
        var tree = Wrapped(node);

        var refusal = Assert.Throws<CapabilityNotSupportedException>(() =>
            StatementCompiler.Compile(tree, new Provider(CapabilitySet.Nothing(), () => throw new InvalidOperationException("a refused tree must not reach the serializer")), StatementKind.Write));

        Assert.Equal(node.Name, refusal.Node);
        Assert.Equal(capability, refusal.Capability);
        Assert.Equal(new[] { capability }, StatementCompiler.RequiredBy(tree));
    }

    [Fact(DisplayName = "an insert, an update and a delete with nothing provider specific declare no capability, so a provider with none still serves them")]
    public void Plain_writes_need_no_capability()
    {
        Node[] plain =
        [
            new Insert(T, [A], [new ValuesRow([new Param(0, 1)])]),
            new Update(T, [new Assignment(A, new Plus(new Coalesce(A, new IntLit(0)), new Param(0, 1)))], new Where([new IsNull(Doc)])),
            new Delete(T, new Where([new IsNull(Doc)])),
        ];

        foreach (var tree in plain) Assert.Empty(StatementCompiler.RequiredBy(tree));
    }

    private static Select Wrapped(Type node) => new([Sample(node)]);

    private static Node Sample(Type node) => node.Name switch
    {
        nameof(Conflict) => new Conflict([A]),
        nameof(Returning) => new Returning([A]),
        nameof(ClockPlusMinutes) => new ClockPlusMinutes(new Param(0, 1)),
        nameof(NamedLock) => new NamedLock(NamedLockKind.Shared, new Param(0, "k")),
        nameof(JsonDocumentOrEmpty) => new JsonDocumentOrEmpty(A),
        nameof(JsonConcat) => new JsonConcat(A, A),
        nameof(JsonRemoveKey) => new JsonRemoveKey(A, A),
        nameof(JsonObjectOf) => new JsonObjectOf(A, A),
        nameof(JsonIntOrZero) => new JsonIntOrZero(A),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private sealed class Provider(CapabilitySet capabilities, Func<ISerializer> serializer) : ISqlProvider
    {
        public string Id => "probe";

        public CapabilitySet Capabilities => capabilities;

        public ISerializer CreateSerializer() => serializer();
    }
}
