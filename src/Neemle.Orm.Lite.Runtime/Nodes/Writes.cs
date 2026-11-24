namespace Neemle.Orm.Lite.Nodes;

public sealed record Assignment(ColumnRef Target, Node Value) : Node
{
    public override IReadOnlyList<Node> Children() => [Target, Value];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ValuesRow(NodeList Values) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Values];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Returning(NodeList Columns) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.Returning];

    public override IReadOnlyList<Node> Children() => [.. Columns];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ConflictUpdate(NodeList Assignments, Node? Where = null) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Assignments, .. Slots(Where)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Conflict(NodeList Target, Node? TargetWhere = null, ConflictUpdate? Update = null) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.Upsert];

    public override IReadOnlyList<Node> Children() => [.. Target, .. Slots(TargetWhere, Update)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Insert(
    TableRef Into,
    NodeList Columns,
    NodeList Rows,
    Conflict? Conflict = null,
    Returning? Returning = null) : Node
{
    public override IReadOnlyList<Node> Children() =>
        [Into, .. Columns, .. Rows, .. Slots(Conflict, Returning)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Update(
    TableRef Table,
    NodeList Assignments,
    Where? Where = null,
    Returning? Returning = null) : Node
{
    public override IReadOnlyList<Node> Children() =>
        [Table, .. Assignments, .. Slots(Where, Returning)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Delete(TableRef From, Where? Where = null, Returning? Returning = null) : Node
{
    public override IReadOnlyList<Node> Children() => [From, .. Slots(Where, Returning)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Plus(Node Left, Node Right) : Node
{
    public override IReadOnlyList<Node> Children() => [Left, Right];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Coalesce(Node Value, Node Fallback) : Node
{
    public override IReadOnlyList<Node> Children() => [Value, Fallback];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
