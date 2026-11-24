using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteSerializer
{
    public override void Visit(BareLit node)
    {
        if (node.ExactDecimal) W.Str(node.Text);
        else base.Visit(node);
    }
}
