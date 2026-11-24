namespace Neemle.OrmLite.Nodes;

public sealed record ColumnRef(string Column, Node? Owner = null, bool Bare = false) : Node
{
    public override IReadOnlyList<Node> Children() => Slots(Owner);

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record And(Node Left, Node Right) : Node
{
    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Or(Node Left, Node Right) : Node
{
    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Not(Node Operand) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Group(Node Inner) : Node
{
    public override IReadOnlyList<Node> Children() => [Inner];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Compare(CompareOp Op, Node Left, Node Right) : Node
{
    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record IsNull(Node Operand) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record IsNotNull(Node Operand) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record InList(Node Operand, NodeList Items) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand, .. Items];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record InSubquery(Node Operand, Select Query) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand, Query];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Like(Node Operand, Node Pattern, char Escape) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand, Pattern];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ILike(Node Operand, Node Pattern, char Escape) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand, Pattern];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Exists(Select Query, bool Negated = false) : Node
{
    public override IReadOnlyList<Node> Children() => [Query];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ScalarSubquery(Select Query) : Node
{
    public override IReadOnlyList<Node> Children() => [Query];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Call(Fn Function, NodeList Arguments) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Arguments];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Aggregate(AggregateFn Function, Node Operand, int Scale = 0) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record CountFiltered(Node Filter) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.AggregateFilter];

    public override IReadOnlyList<Node> Children() => [Filter];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Cast(Node Operand, CastType Type) : Node
{
    public override IReadOnlyList<Capability> Requires() =>
        Type == CastType.Json ? [Capability.JsonDocument] : [];

    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record CaseWhen(Node Condition, Node Result, Node? Otherwise = null) : Node
{
    public override IReadOnlyList<Node> Children() => Slots(Condition, Result, Otherwise);

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Aliased(Node Operand, string Alias) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
