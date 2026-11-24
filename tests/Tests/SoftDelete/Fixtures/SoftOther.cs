using System.Data.Common;

namespace Neemle.OrmLite.Tests.SoftDelete.Fixtures;

public sealed class SoftOther
{
    public const string Table = "soft_others";
    public static readonly string[] Columns = ["id", "name", "deleted_at"];

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Name) => "name",
        nameof(DeletedAt) => "deleted_at",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a SoftOther member"),
    };

    public static SoftOther Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Name = r.GetString(1),
        DeletedAt = r.IsDBNull(2) ? null : r.GetDateTime(2),
    };
}
