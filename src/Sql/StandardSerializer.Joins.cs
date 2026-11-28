using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sql;

public abstract partial class StandardSerializer : ILeftJoinNodeVisitor
{
    public virtual void Visit(LeftJoin node)
    {
        W.Atom("LEFT").Kw(Kw.Join);
        Write(node.Table);
        W.Kw(Kw.On);
        Write(node.On);
    }
}
