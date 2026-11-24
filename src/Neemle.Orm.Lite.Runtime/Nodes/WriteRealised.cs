namespace Neemle.Orm.Lite.Nodes;

public sealed record ClockPlusMinutes(Node Minutes) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.IntervalArithmetic];

    public override IReadOnlyList<Node> Children() => [Minutes];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record NamedLock(NamedLockKind Kind, Node Key) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.NamedLock];

    public override IReadOnlyList<Node> Children() => [Key];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonDocumentOrEmpty(Node Document) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonConcat(Node Left, Node Right) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonRemoveKey(Node Document, Node Key) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Document, Key];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonObjectOf(Node Key, Node Value) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Key, Value];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record JsonIntOrZero(Node Text) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.JsonDocument];

    public override IReadOnlyList<Node> Children() => [Text];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
