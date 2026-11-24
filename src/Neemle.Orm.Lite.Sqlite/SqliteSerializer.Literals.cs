using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

public sealed partial class SqliteSerializer
{
    public override void Visit(BareLit node)
    {
        if (node.ExactDecimal) W.Str(node.Text);
        else base.Visit(node);
    }
}
