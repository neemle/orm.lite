using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public static class QueryProbeWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<QueryProbe>(QueryProbe.Table, QueryProbe.Column, QueryProbe.Columns);
            _registered = true;
        }
    }

    public static Node Schema() => new CreateTable(
        QueryProbe.Table,
        [
            Col("id", ColumnType.Uuid, false, true),
            Col("band", ColumnType.Int32, false),
            Col("label", ColumnType.Text),
            Col("due", ColumnType.Int32),
            Col("tier", ColumnType.Text, false),
            Col("alt", ColumnType.Text),
            Col("weight", ColumnType.Double, false),
            Col("hits", ColumnType.Int32),
            Col("secret", ColumnType.Text),
        ]);

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false) =>
        new(name, new TypeRef(type), nullable, key);
}
