using System.Reflection;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class NodeTreeTests
{
    private static IReadOnlyList<Type> ConcreteNodeTypes() =>
        typeof(Node).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Node)) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static Select Query(int ordinal = 0, string table = "notes", bool descending = false) =>
        new(
            [new ColumnRef("id"), new Aliased(new NullLit(), "body")],
            new TableRef(table),
            new Where([
                new IsNull(new ColumnRef("deleted_at")),
                new And(
                    new Compare(CompareOp.Greater, new ColumnRef("rank"), new Param(ordinal, 1)),
                    new InList(new ColumnRef("id"), [new Param(ordinal + 1, "a"), new Param(ordinal + 2, "b")])),
            ]),
            new OrderBy([new OrderTerm(new ColumnRef("title"), descending, NullsPlacement.Last)]),
            new Limit(10));

    public class Recorder : DispatchProxy
    {
        public List<(Type Declared, Type Seen)> Calls { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add((targetMethod!.GetParameters()[0].ParameterType, args![0]!.GetType()));
            return null;
        }
    }

    [Fact(DisplayName = "the roster holds one sample of every concrete node type, so a node added without a sample fails here")]
    public void The_roster_covers_every_concrete_node()
    {
        var sampled = NodeRoster.Samples.Select(n => n.GetType()).Distinct().OrderBy(t => t.Name, StringComparer.Ordinal).ToList();

        Assert.Equal(ConcreteNodeTypes(), sampled);
        Assert.Equal(sampled.Count, NodeRoster.Samples.Count);
    }

    [Fact(DisplayName = "the visitor is closed: it has exactly one Visit overload per concrete node, so a provider cannot forget one and no node is missing a rule")]
    public void The_visitor_has_one_overload_per_node()
    {
        var overloads = typeof(INodeVisitor).GetMethods().Select(m => m.GetParameters().Single().ParameterType).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();

        Assert.Equal(ConcreteNodeTypes(), overloads);
    }

    [Fact(DisplayName = "every node dispatches to the visitor overload of its own type")]
    public void Every_node_dispatches_to_its_own_overload()
    {
        var proxy = DispatchProxy.Create<INodeVisitor, Recorder>();
        var recorder = (Recorder)(object)proxy;

        foreach (var node in NodeRoster.Samples) node.Accept(proxy);

        Assert.Equal(NodeRoster.Samples.Count, recorder.Calls.Count);
        Assert.All(recorder.Calls, call => Assert.Equal(call.Declared, call.Seen));
    }

    [Fact(DisplayName = "a node's name is its own type name and nothing else")]
    public void A_node_name_is_its_type_name()
    {
        Assert.All(NodeRoster.Samples, node => Assert.Equal(node.GetType().Name, node.Name));
        Assert.Equal(nameof(Compare), new Compare(CompareOp.Equal, new Star(), new Star()).Name);
    }

    [Fact(DisplayName = "two trees built separately from the same data are equal and hash alike, and each differing slot makes them unequal")]
    public void Trees_compare_structurally()
    {
        Assert.Equal(Query(), Query());
        Assert.Equal(Query().GetHashCode(), Query().GetHashCode());
        Assert.NotSame(Query(), Query());

        Assert.NotEqual(Query(), Query(ordinal: 1));
        Assert.NotEqual(Query(), Query(table: "other"));
        Assert.NotEqual(Query(), Query(descending: true));
        Assert.NotEqual(Query(), Query() with { Limit = new Limit(11) });
        Assert.NotEqual(Query(), Query() with { Limit = null });
        Assert.NotEqual(Query(), Query() with { Offset = new Offset(0) });
        Assert.NotEqual(Query(), Query() with { Lock = new RowLock(RowLockStrength.Update) });
    }

    [Fact(DisplayName = "operand order is part of the tree: And(a, b) is not And(b, a), and a comparison operator is part of it too")]
    public void Operand_order_and_operator_are_part_of_the_tree()
    {
        var a = new ColumnRef("a");
        var b = new ColumnRef("b");

        Assert.NotEqual<Node>(new And(a, b), new And(b, a));
        Assert.NotEqual<Node>(new And(a, b), new Or(a, b));
        Assert.NotEqual<Node>(new Compare(CompareOp.Less, a, b), new Compare(CompareOp.LessOrEqual, a, b));
        Assert.NotEqual<Node>(new IsNull(a), new IsNotNull(a));
        Assert.NotEqual<Node>(new Like(a, b, '~'), new ILike(a, b, '~'));
        Assert.NotEqual<Node>(new Like(a, b, '~'), new Like(a, b, '!'));
    }

    [Fact(DisplayName = "a node list compares by its elements in order, and its text form is bounded")]
    public void A_node_list_compares_by_elements()
    {
        var one = new ColumnRef("a");
        var two = new ColumnRef("b");

        Assert.Equal(NodeList.Create([one, two]), NodeList.Of([new ColumnRef("a"), new ColumnRef("b")]));
        Assert.NotEqual(NodeList.Create([one, two]), NodeList.Create([two, one]));
        Assert.NotEqual(NodeList.Create([one]), NodeList.Create([one, two]));
        Assert.Equal(NodeList.Empty, NodeList.Create([]));
        Assert.Equal(2, NodeList.Create([one, two]).Count);
        Assert.Equal(two, NodeList.Create([one, two])[1]);
    }

    [Fact(DisplayName = "a parameter node compares by ordinal and by value, and a collection value compares by its elements while a string does not")]
    public void Parameter_values_compare_structurally()
    {
        Assert.Equal(new Param(0, new[] { 1, 2 }), new Param(0, new List<int> { 1, 2 }));
        Assert.Equal(new Param(0, new[] { 1, 2 }).GetHashCode(), new Param(0, new List<int> { 1, 2 }).GetHashCode());
        Assert.NotEqual(new Param(0, new[] { 1, 2 }), new Param(0, new[] { 1, 3 }));
        Assert.NotEqual(new Param(0, new[] { 1, 2 }), new Param(0, new[] { 1, 2, 3 }));
        Assert.NotEqual(new Param(0, "ab"), new Param(0, "ac"));
        Assert.Equal(new Param(0, "ab"), new Param(0, "ab"));
        Assert.NotEqual(new Param(0, "ab"), new Param(1, "ab"));
        Assert.NotEqual(new Param(0, null), new Param(0, "ab"));
        Assert.Equal(new Param(3, null), new Param(3, null));
        Assert.NotEqual(new Param(0, 1), new Param(0, 1L));
    }

    [Fact(DisplayName = "a node is immutable: a with-copy is a new tree and the original is untouched")]
    public void A_node_is_immutable()
    {
        var original = Query();

        var changed = original with { Limit = new Limit(99) };

        Assert.Equal(new Limit(10), original.Limit);
        Assert.Equal(new Limit(99), changed.Limit);
        Assert.NotEqual(original, changed);
    }

    [Fact(DisplayName = "a deep tree prints in size linear in its depth, because the record text form does not repeat a subtree through its child list")]
    public void A_deep_tree_prints_in_linear_size()
    {
        Node deep = new ColumnRef("c");
        for (var i = 0; i < 20; i++) deep = new Not(deep);

        var text = deep.ToString();

        Assert.True(text.Length < 2_000, "text length " + text.Length);
    }

    [Fact(DisplayName = "children are the child slots in order, the optional ones only when present")]
    public void Children_are_the_slots_in_order()
    {
        var select = Query();

        var children = select.Children();

        Assert.Equal(6, children.Count);
        Assert.Equal(select.Columns[0], children[0]);
        Assert.Equal(select.Columns[1], children[1]);
        Assert.Equal(select.From, children[2]);
        Assert.Equal(select.Where, children[3]);
        Assert.Equal(select.OrderBy, children[4]);
        Assert.Equal(select.Limit, children[5]);
        Assert.Equal(5, (select with { Limit = null }).Children().Count);

        Assert.Empty(new Star().Children());
        Assert.Empty(new Param(0, 1).Children());
        Assert.Empty(new ColumnRef("c").Children());
        Assert.Single(new ColumnRef("c", new AliasRef("s0")).Children());
        Assert.Equal(2, new CaseWhen(new BoolLit(true), new NullLit()).Children().Count);
        Assert.Equal(3, new CaseWhen(new BoolLit(true), new NullLit(), new NullLit()).Children().Count);
    }

    [Fact(DisplayName = "a node declares the capabilities it needs, and a node that needs none declares none")]
    public void Nodes_declare_the_capabilities_they_need()
    {
        Assert.Equal(new[] { Capability.ArrayParameter }, new AnyOf(new Star(), new Star()).Requires());
        Assert.Equal(new[] { Capability.NullSafeEquality }, new NullSafeEquals(new Star(), new Star()).Requires());
        Assert.Equal(new[] { Capability.AggregateFilter }, new CountFiltered(new Star()).Requires());
        Assert.Equal(new[] { Capability.RowLock }, new RowLock(RowLockStrength.Update).Requires());
        Assert.Equal(new[] { Capability.RowLock, Capability.KeyPreservingRowLock }, new RowLock(RowLockStrength.KeyPreserving).Requires());
        Assert.Equal(new[] { Capability.JsonDocument }, new Cast(new Star(), CastType.Json).Requires());
        Assert.Empty(new Cast(new Star(), CastType.Text).Requires());
        Assert.Equal(new[] { Capability.JsonDocument }, new JsonKeyText(new Star(), new Star()).Requires());
        Assert.Equal(new[] { Capability.JsonDocument }, new Comparable(new Star(), JsonValueOrder.Text).Requires());
        Assert.Empty(new And(new Star(), new Star()).Requires());
        Assert.Empty(new Select([new Star()], new TableRef("t")).Requires());
    }

    [Fact(DisplayName = "the capabilities a whole tree needs are the distinct union over every node, wherever it sits")]
    public void A_tree_needs_the_union_of_its_nodes()
    {
        var tree = new Select(
            [new CountFiltered(new Group(new AnyOf(new ColumnRef("id"), new Param(0, new[] { 1 }))))],
            new TableRef("t"),
            new Where([new NullSafeEquals(new ColumnRef("a"), new Param(1, null)), new AnyOf(new ColumnRef("b"), new Param(2, new[] { 2 }))]),
            Lock: new RowLock(RowLockStrength.KeyPreserving));

        var needed = StatementCompiler.RequiredBy(tree);

        Assert.Equal(
            new[] { Capability.AggregateFilter, Capability.ArrayParameter, Capability.NullSafeEquality, Capability.RowLock, Capability.KeyPreservingRowLock },
            needed);
        Assert.Empty(StatementCompiler.RequiredBy(new Select([new Star()], new TableRef("t"))));
    }
}
