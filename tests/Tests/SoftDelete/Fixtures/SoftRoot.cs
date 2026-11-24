using System.Data.Common;

namespace Neemle.OrmLite.Tests.SoftDelete.Fixtures;

public sealed class SoftRoot
{
    public const string Table = "soft_roots";
    public static readonly string[] Columns = ["id", "name", "rank", "deleted_at"];

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public DateTime? DeletedAt { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Name) => "name",
        nameof(Rank) => "rank",
        nameof(DeletedAt) => "deleted_at",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a SoftRoot member"),
    };

    public static SoftRoot Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Name = r.GetString(1),
        Rank = r.GetInt32(2),
        DeletedAt = r.IsDBNull(3) ? null : r.GetDateTime(3),
    };
}
