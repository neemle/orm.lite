using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class ScalarProbe
{
    public const string Table = "scalar_probe_rows";

    public static readonly string[] Columns =
    [
        "id", "rank", "big", "ratio", "price", "opt_price", "flag", "other_id",
        "stamp", "opt_stamp", "day", "mood", "opt_mood", "note", "opt_rank",
    ];

    public Guid Id { get; set; }

    public int Rank { get; set; }

    public long Big { get; set; }

    public double Ratio { get; set; }

    public decimal Price { get; set; }

    public decimal? OptPrice { get; set; }

    public bool Flag { get; set; }

    public Guid? OtherId { get; set; }

    public DateTime Stamp { get; set; }

    public DateTime? OptStamp { get; set; }

    public DateOnly Day { get; set; }

    public ScalarMood Mood { get; set; }

    public ScalarMood? OptMood { get; set; }

    public string Note { get; set; } = "";

    public int? OptRank { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(Rank) => "rank",
        nameof(Big) => "big",
        nameof(Ratio) => "ratio",
        nameof(Price) => "price",
        nameof(OptPrice) => "opt_price",
        nameof(Flag) => "flag",
        nameof(OtherId) => "other_id",
        nameof(Stamp) => "stamp",
        nameof(OptStamp) => "opt_stamp",
        nameof(Day) => "day",
        nameof(Mood) => "mood",
        nameof(OptMood) => "opt_mood",
        nameof(Note) => "note",
        nameof(OptRank) => "opt_rank",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a ScalarProbe member"),
    };

    public static ScalarProbe Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Rank = r.GetInt32(1),
        Big = r.GetInt64(2),
        Ratio = r.GetDouble(3),
        Price = r.GetDecimal(4),
        OptPrice = r.IsDBNull(5) ? null : r.GetDecimal(5),
        Flag = r.GetBoolean(6),
        OtherId = r.IsDBNull(7) ? null : r.GetGuid(7),
        Stamp = r.GetDateTime(8),
        OptStamp = r.IsDBNull(9) ? null : r.GetDateTime(9),
        Day = r.GetFieldValue<DateOnly>(10),
        Mood = OrmEnum.Read<ScalarMood>(r, 11, Table, "mood", "id"),
        OptMood = r.IsDBNull(12) ? null : OrmEnum.Read<ScalarMood>(r, 12, Table, "opt_mood", "id"),
        Note = r.GetString(13),
        OptRank = r.IsDBNull(14) ? null : r.GetInt32(14),
    };
}
