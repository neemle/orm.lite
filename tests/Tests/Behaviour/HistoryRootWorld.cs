using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Behaviour;

public static class HistoryRootWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static readonly IReadOnlyList<RowHistoryTable> Tables =
    [
        new RowHistoryTable(HistRoot.Table, "id", "deleted_at", "id", ["updated_at"]),
    ];

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<HistRoot>(HistRoot.Table, HistRoot.Column, HistRoot.Columns, new SoftDeleteMeta(
                "DeletedAt",
                TenantColumn: "id",
                Unversioned: ["updated_at"],
                KeyColumn: "id",
                LabelColumn: "name",
                EntityToken: "hist_root",
                PropertyColumns: Pairs(),
                StatusColumns: ["status"]));
            _registered = true;
        }
    }

    public static Node Schema() =>
        new CreateTable(
            HistRoot.Table,
            [
                Col("id", ColumnType.Uuid, false, true), Col("name", ColumnType.Text, false),
                Col("status", ColumnType.Text), Col("updated_at", ColumnType.Timestamp),
                Col("deleted_at", ColumnType.Timestamp),
            ]);

    private static string[][] Pairs() =>
        typeof(HistRoot).GetProperties().OrderBy(p => p.MetadataToken)
            .Select(p => new[] { p.Name, HistoryWorld.Snake(p.Name) }).ToArray();

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false) =>
        new(name, new TypeRef(type), nullable, key);
}
