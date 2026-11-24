using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// A tenant-root entity: its tenant column is its key column (the row IS the tenant), the shape a projects or tenants
// table takes when it is keyed by the tenant id.
public sealed class HistRoot
{
    public const string Table = "hist_roots";

    public static readonly string[] Columns = ["id", "name", "status", "updated_at", "deleted_at"];

    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? Status { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => HistoryWorld.Snake(property);

    public static HistRoot Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Name = r.GetString(1),
        Status = r.IsDBNull(2) ? null : r.GetString(2),
        UpdatedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
        DeletedAt = r.IsDBNull(4) ? null : r.GetDateTime(4),
    };
}
