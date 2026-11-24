using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Behaviour;

public static class EnumProbeWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<EnumProbe>(EnumProbe.Table, EnumProbe.Column, EnumProbe.Columns);
            _registered = true;
        }
    }

    public static Node Schema() => new CreateTable(
        EnumProbe.Table,
        [
            Col("id", ColumnType.Uuid, false, true),
            Col("tier", ColumnType.Text, false),
            Col("rank", ColumnType.Int32, false),
        ]);

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false) =>
        new(name, new TypeRef(type), nullable, key);
}
