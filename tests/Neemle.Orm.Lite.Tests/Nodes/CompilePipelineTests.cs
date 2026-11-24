using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sql;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class CompilePipelineTests
{
    private sealed class Probe(CapabilitySet capabilities, Func<Node, Node>? lower = null, Func<ISerializer>? serializer = null) : ISqlProvider
    {
        public int Serialized { get; private set; }

        public int Lowered { get; private set; }

        public string Id => "probe";

        public CapabilitySet Capabilities { get; } = capabilities;

        public ISerializer CreateSerializer()
        {
            Serialized++;
            return serializer is null ? new FixtureSerializer() : serializer();
        }

        public Node Lower(Node tree)
        {
            Lowered++;
            return lower is null ? tree : lower(tree);
        }
    }

    private static readonly Select Plain = new([new Star()], new TableRef("t"), Limit: new Limit(10));

    private static Select WithAnyOf() =>
        new([new Star()], new TableRef("t"), new Where([new AnyOf(new ColumnRef("id"), new Param(0, new[] { 1 }))]));

    [Fact(DisplayName = "a node whose capability the provider does not have is refused by name before the serializer is created or any text is written")]
    public void A_missing_capability_is_refused_before_serialization()
    {
        var provider = new Probe(CapabilitySet.Nothing());

        var refusal = Assert.Throws<CapabilityNotSupportedException>(() =>
            StatementCompiler.Compile(WithAnyOf(), provider, StatementKind.Read, "t"));

        Assert.Equal("probe", refusal.Provider);
        Assert.Equal(nameof(AnyOf), refusal.Node);
        Assert.Equal(Capability.ArrayParameter, refusal.Capability);
        Assert.Equal("t", refusal.Table);
        Assert.Contains("probe", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("ArrayParameter", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("AnyOf", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'t'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, provider.Serialized);
        Assert.Equal(0, provider.Lowered);
    }

    [Fact(DisplayName = "a capability served natively or by emulation does not refuse, and only None does")]
    public void Only_none_refuses()
    {
        foreach (var level in new[] { Support.Native, Support.Emulated })
        {
            var provider = new Probe(CapabilitySet.Everything(level), serializer: () => new PostgresSerializer());
            var allowed = StatementCompiler.Compile(WithAnyOf(), provider, StatementKind.Read);
            Assert.Equal("SELECT * FROM \"t\" WHERE \"id\" = ANY(@p0)", allowed.Text);
        }

        var none = new Probe(CapabilitySet.Everything().With(Capability.ArrayParameter, Support.None));
        Assert.Throws<CapabilityNotSupportedException>(() => StatementCompiler.Compile(WithAnyOf(), none, StatementKind.Read));
        var partial = new Probe(CapabilitySet.Nothing().With(Capability.NullSafeEquality, Support.Emulated));
        Assert.Throws<CapabilityNotSupportedException>(() => StatementCompiler.Compile(WithAnyOf(), partial, StatementKind.Read));
    }

    [Fact(DisplayName = "a requirement deep inside the tree is found, and the refusal names the capability that is missing, not the first one the provider lacks")]
    public void A_deep_requirement_is_found()
    {
        var tree = new Select(
            [new Aliased(new Group(new Not(new And(new Star(), new CountFiltered(new NullSafeEquals(new ColumnRef("a"), new Param(0, null)))))), "x")],
            new TableRef("t"));
        var provider = new Probe(CapabilitySet.Everything().With(Capability.NullSafeEquality, Support.None));

        var refusal = Assert.Throws<CapabilityNotSupportedException>(() => StatementCompiler.Compile(tree, provider, StatementKind.Read));

        Assert.Equal(Capability.NullSafeEquality, refusal.Capability);
        Assert.Equal(nameof(NullSafeEquals), refusal.Node);
        Assert.Null(refusal.Table);
    }

    [Fact(DisplayName = "the pipeline checks capabilities, then lowers, then writes, and the lowered tree is the one that is written")]
    public void Lowering_runs_between_the_check_and_the_write()
    {
        var provider = new Probe(CapabilitySet.Everything(), tree => ((Select)tree) with { Limit = new Limit(1) });

        var statement = StatementCompiler.Compile(Plain, provider, StatementKind.Read);

        Assert.Equal("SELECT * FROM \"t\" LIMIT 1", statement.Text);
        Assert.Equal(1, provider.Lowered);
        Assert.Equal(1, provider.Serialized);
    }

    [Fact(DisplayName = "the capability check is made on the tree as built, so a lowering that would remove the node cannot hide a refusal")]
    public void A_lowering_cannot_hide_a_refusal()
    {
        var provider = new Probe(CapabilitySet.Nothing(), _ => Plain);

        Assert.Throws<CapabilityNotSupportedException>(() => StatementCompiler.Compile(WithAnyOf(), provider, StatementKind.Read));
        Assert.Equal(0, provider.Lowered);
    }

    [Fact(DisplayName = "the statement kind is carried through unchanged")]
    public void The_kind_is_carried_through()
    {
        var provider = new Probe(CapabilitySet.Everything());

        Assert.Equal(StatementKind.Write, StatementCompiler.Compile(Plain, provider, StatementKind.Write).Kind);
        Assert.Equal(StatementKind.Ddl, StatementCompiler.Compile(Plain, provider, StatementKind.Ddl).Kind);
        Assert.Equal(StatementKind.Read, StatementCompiler.Compile(Plain, provider, StatementKind.Read).Kind);
    }

    [Fact(DisplayName = "two different values under one parameter ordinal are refused, and the same value twice is one parameter")]
    public void A_parameter_ordinal_has_one_value()
    {
        var provider = new Probe(CapabilitySet.Everything());
        var clash = new Select(
            [new Param(0, "a"), new Param(0, "b")],
            new TableRef("t"));
        var same = new Select([new Param(0, "a"), new Param(0, "a")], new TableRef("t"));

        Assert.Throws<InvalidOperationException>(() => StatementCompiler.Compile(clash, provider, StatementKind.Read));
        Assert.Single(StatementCompiler.Compile(same, provider, StatementKind.Read).Parameters);
    }

    [Fact(DisplayName = "a null tree or a null provider is refused")]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => StatementCompiler.Compile(null!, new Probe(CapabilitySet.Everything()), StatementKind.Read));
        Assert.Throws<ArgumentNullException>(() => StatementCompiler.Compile(Plain, null!, StatementKind.Read));
    }

    [Fact(DisplayName = "the standard grammar renders what every provider shares, and refuses what only a provider can spell")]
    public void The_standard_grammar_renders_the_shared_part()
    {
        var provider = new Probe(CapabilitySet.Everything());

        Assert.Equal("CURRENT_TIMESTAMP", StatementCompiler.Compile(new ClockNow(), provider, StatementKind.Read).Text);
        Assert.Equal("\"a\" IS NOT DISTINCT FROM @p0", StatementCompiler.Compile(new NullSafeEquals(new ColumnRef("a"), new Param(0, null)), provider, StatementKind.Read).Text);
        Assert.Equal("CAST(@p0 AS text)", StatementCompiler.Compile(new Cast(new Param(0, 1), CastType.Text), provider, StatementKind.Read).Text);
        Assert.Equal("CAST(@p0 AS double precision)", StatementCompiler.Compile(new Cast(new Param(0, 1.5), CastType.Double), provider, StatementKind.Read).Text);
        Assert.Equal("CAST(@p0 AS numeric)", StatementCompiler.Compile(new Cast(new Param(0, 1m), CastType.Decimal), provider, StatementKind.Read).Text);
        Assert.Equal("SELECT * FROM \"t\" FOR UPDATE", StatementCompiler.Compile(new Select([new Star()], new TableRef("t"), Lock: new RowLock(RowLockStrength.Update)), provider, StatementKind.Read).Text);
        Assert.Throws<NotSupportedException>(() => StatementCompiler.Compile(new Cast(new Param(0, "{}"), CastType.Json), provider, StatementKind.Read));
        Assert.Throws<NotSupportedException>(() => StatementCompiler.Compile(new Select([new Star()], new TableRef("t"), Lock: new RowLock(RowLockStrength.KeyPreserving)), provider, StatementKind.Read));
        Assert.Throws<NotSupportedException>(() => StatementCompiler.Compile(new AnyOf(new ColumnRef("a"), new Param(0, new[] { 1 })), provider, StatementKind.Read));
    }

    [Fact(DisplayName = "the capability set answers None for a capability it was never given, and With returns a changed copy")]
    public void The_capability_set_is_immutable_data()
    {
        var all = CapabilitySet.Everything();
        var changed = all.With(Capability.RowLock, Support.Emulated);

        Assert.Equal(Support.Native, all.Of(Capability.RowLock));
        Assert.Equal(Support.Emulated, changed.Of(Capability.RowLock));
        Assert.Equal(Support.Native, changed.Of(Capability.JsonDocument));
        Assert.All(Enum.GetValues<Capability>(), c => Assert.Equal(Support.None, CapabilitySet.Nothing().Of(c)));
        Assert.All(Enum.GetValues<Capability>(), c => Assert.Equal(Support.Native, all.Of(c)));
    }

    [Fact(DisplayName = "the writer spaces tokens by their kind: attached calls and operators stay tight, keywords and atoms stand apart")]
    public void The_writer_spaces_tokens_by_kind()
    {
        var text = new SqlWriter()
            .Kw(Kw.Select).Func("count").Atom("*").Close().Kw(Kw.From).Ident("t").Dot().Ident("c")
            .Comma().Open().Ident("x").Close().Attach("::").Atom("int").ToString();

        Assert.Equal("SELECT count(*) FROM \"t\".\"c\", (\"x\")::int", text);
        Assert.Equal("CURRENT_TIMESTAMP", new SqlWriter().Kw(Kw.CurrentTimestamp).ToString());
        Assert.Equal("ORDER BY", new SqlWriter().Kw(Kw.Order).Kw(Kw.By).ToString());
        Assert.Equal("'a''b'", new SqlWriter().Str("a'b").ToString());
        Assert.Equal("@p3", new SqlWriter().Param("@", "p3").ToString());
    }
}
