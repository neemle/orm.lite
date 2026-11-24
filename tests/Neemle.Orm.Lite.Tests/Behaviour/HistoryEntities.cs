using System.Data.Common;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class HistProject
{
    public const string Table = "hist_projects";

    public static readonly string[] Columns = ["id", "tenant_id", "name", "status", "updated_at", "deleted_at"];

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = "";

    public string? Status { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => HistoryWorld.Snake(property);

    public static HistProject Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        TenantId = r.GetGuid(1),
        Name = r.GetString(2),
        Status = r.IsDBNull(3) ? null : r.GetString(3),
        UpdatedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
        DeletedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
    };
}

public sealed class HistTask
{
    public const string Table = "hist_tasks";

    public static readonly string[] Columns = ["id", "tenant_id", "project_id", "name", "rank", "status", "updated_at", "deleted_at"];

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = "";

    public int Rank { get; set; }

    public string? Status { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => HistoryWorld.Snake(property);

    public static HistTask Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        TenantId = r.GetGuid(1),
        ProjectId = r.IsDBNull(2) ? null : r.GetGuid(2),
        Name = r.GetString(3),
        Rank = r.GetInt32(4),
        Status = r.IsDBNull(5) ? null : r.GetString(5),
        UpdatedAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
        DeletedAt = r.IsDBNull(7) ? null : r.GetDateTime(7),
    };
}

public sealed class HistItem
{
    public const string Table = "hist_items";

    public static readonly string[] Columns = ["id", "code", "deleted_at"];

    public Guid Id { get; set; }

    public string Code { get; set; } = "";

    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => HistoryWorld.Snake(property);

    public static HistItem Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Code = r.GetString(1),
        DeletedAt = r.IsDBNull(2) ? null : r.GetDateTime(2),
    };
}

public static class HistoryWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static readonly IReadOnlyList<RowHistoryTable> Tables =
    [
        new RowHistoryTable(HistItem.Table, "id", "deleted_at", "", []),
        new RowHistoryTable(HistProject.Table, "id", "deleted_at", "tenant_id", ["updated_at"]),
        new RowHistoryTable(HistTask.Table, "id", "deleted_at", "tenant_id", ["updated_at"]),
    ];

    public static string Snake(string property)
    {
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < property.Length; i++)
        {
            if (char.IsUpper(property[i]))
            {
                if (i > 0) text.Append('_');
                text.Append(char.ToLowerInvariant(property[i]));
            }
            else text.Append(property[i]);
        }
        return text.ToString();
    }

    public static ISoftEntry Entry<T>() =>
        OrmMeta.TryGetSoftEntry(typeof(T), out var entry) ? entry : throw new InvalidOperationException(typeof(T).Name + " is not registered as soft");

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<HistProject>(HistProject.Table, HistProject.Column, HistProject.Columns, new SoftDeleteMeta(
                "DeletedAt",
                TenantColumn: "tenant_id",
                Unversioned: ["updated_at"],
                KeyColumn: "id",
                LabelColumn: "name",
                EntityToken: "project",
                PropertyColumns: Pairs<HistProject>(),
                StatusColumns: ["status"]));
            OrmMeta.Register<HistTask>(HistTask.Table, HistTask.Column, HistTask.Columns, new SoftDeleteMeta(
                "DeletedAt",
                TenantColumn: "tenant_id",
                Unversioned: ["updated_at"],
                KeyColumn: "id",
                LabelColumn: "name",
                Owners: ["project_id"],
                EntityToken: "hist_task",
                PropertyColumns: Pairs<HistTask>(),
                PinnedColumns: ["project_id"],
                StatusColumns: ["status"]));
            OrmMeta.Register<HistItem>(HistItem.Table, HistItem.Column, HistItem.Columns, new SoftDeleteMeta(
                "DeletedAt",
                Platform: true,
                KeyColumn: "id",
                LabelColumn: "code",
                EntityToken: "hist_item",
                PropertyColumns: Pairs<HistItem>()));
            _registered = true;
        }
    }

    private static string[][] Pairs<T>() =>
        typeof(T).GetProperties().OrderBy(p => p.MetadataToken).Select(p => new[] { p.Name, Snake(p.Name) }).ToArray();

    public static IReadOnlyList<Node> Schema() =>
    [
        new CreateTable(HistItem.Table, [Col("id", ColumnType.Uuid, false, true), Col("code", ColumnType.Text, false), Col("deleted_at", ColumnType.Timestamp)]),
        new CreateTable(
            HistProject.Table,
            [
                Col("id", ColumnType.Uuid, false, true), Col("tenant_id", ColumnType.Uuid, false), Col("name", ColumnType.Text, false),
                Col("status", ColumnType.Text), Col("updated_at", ColumnType.Timestamp), Col("deleted_at", ColumnType.Timestamp),
            ]),
        new CreateTable(
            HistTask.Table,
            [
                Col("id", ColumnType.Uuid, false, true), Col("tenant_id", ColumnType.Uuid, false), Col("project_id", ColumnType.Uuid),
                Col("name", ColumnType.Text, false), Col("rank", ColumnType.Int32, false), Col("status", ColumnType.Text),
                Col("updated_at", ColumnType.Timestamp), Col("deleted_at", ColumnType.Timestamp),
            ]),
        new CreateIndex("ix_hist_tasks_tenant_name", HistTask.Table, [new ColumnRef("tenant_id"), new ColumnRef("name")], true, new Where([new IsNull(new ColumnRef("deleted_at"))])),
    ];

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false) =>
        new(name, new TypeRef(type), nullable, key);
}
