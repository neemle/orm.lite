using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class ClosureProbe
{
    public const string Table = "closure_probe_rows";

    public static readonly string[] Columns = ["id", "active", "rank", "mood"];

    public Guid Id { get; set; }

    public bool Active { get; set; }

    public int Rank { get; set; }

    public ClosureMood Mood { get; set; }

    public static string Column(string property) => property.ToLowerInvariant();

    public static ClosureProbe Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Active = r.GetBoolean(1),
        Rank = r.GetInt32(2),
        Mood = Enum.Parse<ClosureMood>(r.GetString(3)),
    };
}
