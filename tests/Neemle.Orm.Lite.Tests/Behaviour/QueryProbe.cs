using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// One table for the query-layer suites that run on both providers: nullable columns of several types, an enum and a
// nullable enum, a double, a nullable counter and a text column to redact. Each test seeds its own Band.
public sealed class QueryProbe
{
    public const string Table = "query_probe_rows";

    public static readonly string[] Columns =
        ["id", "band", "label", "due", "tier", "alt", "weight", "hits", "secret"];

    public Guid Id { get; set; }

    public int Band { get; set; }

    public string? Label { get; set; }

    public int? Due { get; set; }

    public EnumTier Tier { get; set; }

    public EnumTier? Alt { get; set; }

    public double Weight { get; set; }

    public int? Hits { get; set; }

    public string? Secret { get; set; }

    public static string Column(string property) => property.ToLowerInvariant();

    public static QueryProbe Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Band = r.GetInt32(1),
        Label = r.IsDBNull(2) ? null : r.GetString(2),
        Due = r.IsDBNull(3) ? null : r.GetInt32(3),
        Tier = Enum.Parse<EnumTier>(r.GetString(4)),
        Alt = r.IsDBNull(5) ? null : Enum.Parse<EnumTier>(r.GetString(5)),
        Weight = r.GetDouble(6),
        Hits = r.IsDBNull(7) ? null : r.GetInt32(7),
        Secret = r.IsDBNull(8) ? null : r.GetString(8),
    };
}
