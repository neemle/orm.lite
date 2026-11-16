using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Fixtures;

public sealed class SoftReferrer
{
    public const string Table = "soft_referrers";
    public static readonly string[] Columns = ["id", "soft_other_id", "name"];

    public Guid Id { get; set; }
    public Guid SoftOtherId { get; set; }
    public string Name { get; set; } = "";

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(SoftOtherId) => "soft_other_id",
        nameof(Name) => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a SoftReferrer member"),
    };

    public static SoftReferrer Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        SoftOtherId = r.GetGuid(1),
        Name = r.GetString(2),
    };
}
