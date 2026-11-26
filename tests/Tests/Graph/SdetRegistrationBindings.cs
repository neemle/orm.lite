using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Graph;

internal static class SdetRegistrationBindings
{
    internal static GraphEntityBinding<SdetRegistryRow> Create(int variant = 0)
    {
        var descriptor = EntityDescriptor.Of("SdetRegistryRow", variant == 1 ? "other_table" : "sdet_registry",
                variant == 2)
            .Property("Id", "id", ColumnType.Int32, false, true)
            .Property("OtherId", "other_id", ColumnType.Int32, true, false)
            .Property("Name", "name", ColumnType.Text, variant != 3, false);
        if (variant == 4) descriptor.View();
        if (variant == 5) descriptor.SoftDelete("Name", "name", "sdet-history");
        if (variant == 6) descriptor.CalculatedLower("Lower", "lower", "Name");
        return new(descriptor, variant == 7 ? AlternateMap : Map,
            new GraphKeyBinding<SdetRegistryRow, int>("id", variant == 8 ? AlternateKey : Key),
            new GraphReferenceBinding<SdetRegistryRow, GraphAuthor, int>("Other", "other_id", OtherKey,
                variant == 9 ? AlternateOther : AssignOther),
            new GraphCollectionBinding<SdetRegistryRow, GraphBook, int>("Children",
                variant == 10 ? "editor_id" : "author_id", Key, ChildKey, AssignChildren),
            new GraphLinkBinding<SdetRegistryRow, GraphTag, int, int>("Tags",
                new(variant == 11 ? "other_links" : "sdet_links", "owner_id", "target_id"), Key, AssignTags));
    }

    internal static GraphEntityBinding<SdetLateRegistryRow> Late() => new(
        EntityDescriptor.Of("SdetLateRegistryRow", "sdet_late", false)
            .Property("Id", "id", ColumnType.Int32, false, true),
        LateMap, new GraphKeyBinding<SdetLateRegistryRow, int>("id", LateKey));

    private static SdetRegistryRow Map(DbDataReader reader) => new() { Id = reader.GetInt32(0) };
    private static SdetRegistryRow AlternateMap(DbDataReader reader) => new() { Id = reader.GetInt32(0) + 1 };
    private static int Key(SdetRegistryRow row) => row.Id;
    private static int AlternateKey(SdetRegistryRow row) => row.Id + 1;
    private static GraphKey<int> OtherKey(SdetRegistryRow row) => new(row.OtherId.HasValue,
        row.OtherId.GetValueOrDefault());
    private static GraphKey<int> ChildKey(GraphBook row) => new(true, row.AuthorId);
    private static void AssignOther(SdetRegistryRow row, GraphAuthor? other) => row.Other = other;
    private static void AlternateOther(SdetRegistryRow row, GraphAuthor? other) => row.Other = null;
    private static void AssignChildren(SdetRegistryRow row, List<GraphBook> children) => row.Children = children;
    private static void AssignTags(SdetRegistryRow row, List<GraphTag> tags) => row.Tags = tags;
    private static SdetLateRegistryRow LateMap(DbDataReader reader) => new() { Id = reader.GetInt32(0) };
    private static int LateKey(SdetLateRegistryRow row) => row.Id;
}
