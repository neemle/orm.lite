using System.Data.Common;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class BehaveNote
{
    public const string Table = "behave_notes";

    public static readonly string[] Columns = ["id", "tenant_id", "title", "stars", "price", "stamped_at", "deleted_at", "doc"];

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Title { get; set; } = "";

    public int Stars { get; set; }

    public decimal? Price { get; set; }

    public DateTime? StampedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Jsonb? Doc { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(TenantId) => "tenant_id",
        nameof(Title) => "title",
        nameof(Stars) => "stars",
        nameof(Price) => "price",
        nameof(StampedAt) => "stamped_at",
        nameof(DeletedAt) => "deleted_at",
        nameof(Doc) => "doc",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a BehaveNote member"),
    };

    public static BehaveNote Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(0),
        TenantId = r.GetGuid(1),
        Title = r.GetString(2),
        Stars = r.GetInt32(3),
        Price = r.IsDBNull(4) ? null : r.GetDecimal(4),
        StampedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
        DeletedAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
        Doc = r.IsDBNull(7) ? null : Jsonb.FromStorage(r.GetString(7)),
    };
}

public sealed class BehaveCounter
{
    public const string Table = "behave_counters";

    public static readonly string[] Columns = ["id", "n"];

    public Guid Id { get; set; }

    public int N { get; set; }

    public static string Column(string property) => property switch
    {
        nameof(Id) => "id",
        nameof(N) => "n",
        _ => throw new ArgumentOutOfRangeException(nameof(property), property, "not a BehaveCounter member"),
    };

    public static BehaveCounter Map(DbDataReader r) => new() { Id = r.GetGuid(0), N = r.GetInt32(1) };
}

internal static class BehaveWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    internal static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<BehaveNote>(
                BehaveNote.Table, BehaveNote.Column, BehaveNote.Columns,
                new SoftDeleteMeta(
                    "DeletedAt",
                    LiveOnlyIndexes: [["tenant_id", "title"]],
                    TenantColumn: "tenant_id",
                    KeyColumn: "id"));
            OrmMeta.Register<BehaveCounter>(BehaveCounter.Table, BehaveCounter.Column, BehaveCounter.Columns);
            _registered = true;
        }
    }
}
