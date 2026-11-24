namespace Neemle.OrmLite.Nodes;

public sealed record AnyOf(Node Operand, Node Values) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.ArrayParameter];

    public override IReadOnlyList<Node> Children() => [Operand, Values];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record NullSafeEquals(Node Left, Node Right) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.NullSafeEquality];

    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Comparable(Node Operand, JsonValueOrder Order) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonKeyText(Node Document, Node Key) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonKeyExists(Node Document, Node Key) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonArrayContainsString(Node Document, Node Key, Node Value) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key, Value];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonArrayContainsAny(Node Document, Node Key, NodeList Values) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key, .. Values];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record OptionRank(NodeList Options, Node Needle) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [.. Options, Needle];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record MovedNumberOrEpochMillis(Node Document, Node Key, Node Timestamp) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key, Timestamp];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
