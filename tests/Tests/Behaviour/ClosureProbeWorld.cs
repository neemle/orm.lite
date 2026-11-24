using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Behaviour;

public static class ClosureProbeWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<ClosureProbe>(ClosureProbe.Table, ClosureProbe.Column, ClosureProbe.Columns);
            _registered = true;
        }
    }

    public static Node Schema() => new CreateTable(
        ClosureProbe.Table,
        [
            Col("id", ColumnType.Uuid, false, true),
            Col("active", ColumnType.Bool, false),
            Col("rank", ColumnType.Int32, false),
            Col("mood", ColumnType.Text, false),
        ]);

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false) =>
        new(name, new TypeRef(type), nullable, key);
}
