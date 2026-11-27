namespace Neemle.OrmLite.Nodes;

public sealed record LeftJoin(TableRef Table, Node On) : Node
{
    public override IReadOnlyList<Node> Children() => [Table, On];

    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        if (visitor is not ILeftJoinNodeVisitor leftVisitor)
            throw new NotSupportedException("Visitor does not support LeftJoin.");
        leftVisitor.Visit(this);
    }
}
