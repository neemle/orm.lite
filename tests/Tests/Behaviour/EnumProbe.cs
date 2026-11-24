using System.Data.Common;

namespace Neemle.OrmLite.Tests.Behaviour;

public sealed class EnumProbe
{
    public const string Table = "enum_probe_rows";

    public static readonly string[] Columns = ["id", "tier", "rank"];

    public Guid Id { get; set; }

    public EnumTier Tier { get; set; }

    public int Rank { get; set; }

    public static string Column(string property) => property.ToLowerInvariant();

    public static EnumProbe Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Tier = Enum.Parse<EnumTier>(r.GetString(1)),
        Rank = r.GetInt32(2),
    };
}
