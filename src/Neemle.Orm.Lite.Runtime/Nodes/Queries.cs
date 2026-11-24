namespace Neemle.Orm.Lite.Nodes;

public sealed record OrderTerm(Node Operand, bool Descending, NullsPlacement Nulls = NullsPlacement.Unspecified, string? Collation = null) : Node
{
    public override IReadOnlyList<Node> Children() => [Operand];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record OrderBy(NodeList Terms) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Terms];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Where(NodeList Terms) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Terms];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Limit(int Count) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Offset(int Count) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Select(
    NodeList Columns,
    TableRef? From = null,
    Where? Where = null,
    OrderBy? OrderBy = null,
    Limit? Limit = null,
    Offset? Offset = null,
    RowLock? Lock = null,
    NodeList? Joins = null,
    NodeList? GroupBy = null,
    Node? Having = null) : Node
{
    public override IReadOnlyList<Node> Children() =>
        [
            .. Columns, .. Slots(From), .. (Joins ?? NodeList.Empty), .. Slots(Where),
            .. (GroupBy ?? NodeList.Empty), .. Slots(Having, OrderBy, Limit, Offset, Lock),
        ];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record InnerJoin(TableRef Table, Node On) : Node
{
    public override IReadOnlyList<Node> Children() => [Table, On];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
