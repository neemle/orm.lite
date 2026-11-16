using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;

public sealed class SoftChild
{
    public const string Table = "soft_children";
    public static readonly string[] Columns = ["id", "soft_root_id", "name", "deleted_at"];

    public Guid Id { get; set; }
    public Guid SoftRootId { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(SoftRootId) => "soft_root_id",
        nameof(Name) => "name",
        nameof(DeletedAt) => "deleted_at",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a SoftChild member"),
    };

    public static SoftChild Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        SoftRootId = r.GetGuid(1),
        Name = r.GetString(2),
        DeletedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
    };
}
