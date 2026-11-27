namespace Neemle.OrmLite.Nodes;

public interface ILeftJoinNodeVisitor : INodeVisitor
{
    void Visit(LeftJoin node);
}
