using System.Text;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Corpus;

public enum CorpusStatus { Draft, Active, Archived }

public sealed class CorpusNote
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public int Rank { get; set; }
    public long Views { get; set; }
    public double Score { get; set; }
    public decimal Price { get; set; }
    public bool Active { get; set; }
    public CorpusStatus Status { get; set; }
    public CorpusStatus? Review { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateOnly? Due { get; set; }
    public Jsonb? Payload { get; set; }
    public Jsonb? Meta { get; set; }
}

public sealed class CorpusTag
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Label { get; set; } = "";
    public Jsonb? Doc { get; set; }
    public int Position { get; set; }
}

public sealed class CorpusTask
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Name { get; set; } = "";
    public int Rank { get; set; }
    public string? Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Jsonb? Doc { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class CorpusPlatformItem
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

public sealed class CorpusProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
}

public sealed class CorpusNoKey
{
    public Guid Ref { get; set; }
    public Guid TenantId { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class CorpusPartial
{
    public Guid Id { get; set; }
    public string Hidden { get; set; } = "";
}

public sealed class CorpusBare
{
    public Guid Id { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class CorpusView
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Rank { get; set; }
}

public sealed class CorpusNarrowView
{
    public Guid Id { get; set; }
}

public sealed class CorpusTotals
{
    public long Items { get; set; }
    public long Points { get; set; }
    public int Smallest { get; set; }
}

public sealed class CorpusUnregistered
{
    public Guid Id { get; set; }
}

public static class CorpusWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    public static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public static readonly Guid Other = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static readonly Guid RowA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");

    public static readonly Guid RowB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");

    public static readonly Guid RowC = Guid.Parse("c0000000-0000-0000-0000-00000000000c");

    public static readonly DateTime Instant = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    public static string Snake(string property)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < property.Length; i++)
        {
            var ch = property[i];
            if (char.IsUpper(ch))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(ch));
            }
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    public static void Ensure()
    {
        lock (Gate)
        {
            if (_registered) return;
            OrmMeta.Register<CorpusNote>("corpus_notes", Snake, Columns<CorpusNote>());
            OrmMeta.Register<CorpusTag>("corpus_tags", Snake, Columns<CorpusTag>());
            OrmMeta.Register<CorpusTask>("corpus_tasks", Snake, Columns<CorpusTask>(), new SoftDeleteMeta(
                "DeletedAt",
                LiveOnlyIndexes: [["tenant_id", "name"]],
                TenantColumn: "tenant_id",
                Unversioned: ["updated_at"],
                KeyColumn: "id",
                LabelColumn: "name",
                Owners: ["project_id"],
                EntityToken: "corpus_task",
                PropertyColumns: PropertyColumns<CorpusTask>(),
                PinnedColumns: ["project_id"],
                StatusColumns: ["status"]));
            OrmMeta.Register<CorpusPlatformItem>("corpus_platform_items", Snake, Columns<CorpusPlatformItem>(), new SoftDeleteMeta(
                "DeletedAt",
                Platform: true,
                KeyColumn: "id",
                LabelColumn: "code",
                EntityToken: "corpus_platform_item",
                PropertyColumns: PropertyColumns<CorpusPlatformItem>()));
            OrmMeta.Register<CorpusProject>("corpus_projects", Snake, Columns<CorpusProject>(), new SoftDeleteMeta(
                "DeletedAt",
                TenantColumn: "id",
                KeyColumn: "id",
                LabelColumn: "name",
                EntityToken: "project",
                PropertyColumns: PropertyColumns<CorpusProject>()));
            OrmMeta.Register<CorpusNoKey>("corpus_no_keys", Snake, Columns<CorpusNoKey>(), new SoftDeleteMeta("DeletedAt", TenantColumn: "tenant_id", EntityToken: "corpus_no_key"));
            OrmMeta.Register<CorpusPartial>("corpus_partials", Snake, ["id"]);
            OrmMeta.Register<CorpusBare>("corpus_bares", Snake, Columns<CorpusBare>(), new SoftDeleteMeta("DeletedAt", Platform: true, KeyColumn: "id", EntityToken: "corpus_bare"));
            OrmMeta.Register<CorpusView>(EntityDescriptor.Of(nameof(CorpusView), "corpus_view", false)
                .Property("Id", "id", ColumnType.Uuid, false, false)
                .Property("Title", "title", ColumnType.Text, false, false)
                .Property("Rank", "rank", ColumnType.Int32, false, false)
                .View());
            OrmMeta.Register<CorpusTotals>(EntityDescriptor.Of(nameof(CorpusTotals), "corpus_totals", false)
                .Property("Items", "items", ColumnType.Int64, false, false)
                .Property("Points", "points", ColumnType.Int64, false, false)
                .Property("Smallest", "smallest", ColumnType.Int32, false, false)
                .View());
            OrmMeta.Register<CorpusNarrowView>(EntityDescriptor.Of(nameof(CorpusNarrowView), "corpus_narrow_view", false)
                .Property("Id", "id", ColumnType.Uuid, false, false)
                .View());
            _registered = true;
        }
    }

    private static string[] Columns<T>() =>
        typeof(T).GetProperties().OrderBy(p => p.MetadataToken).Select(p => Snake(p.Name)).ToArray();

    private static string[][] PropertyColumns<T>() =>
        typeof(T).GetProperties().OrderBy(p => p.MetadataToken).Select(p => new[] { p.Name, Snake(p.Name) }).ToArray();

    public static ISoftEntry Soft<T>() =>
        OrmMeta.TryGetSoftEntry(typeof(T), out var entry) ? entry : throw new InvalidOperationException(typeof(T).Name + " is not registered as soft");

    public static Query<T> Of<T>(OrmContext ctx) where T : class =>
        new(ctx, _ => throw new NotSupportedException("the corpus maps no rows"));
}
