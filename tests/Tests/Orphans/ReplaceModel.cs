using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

// An owned collection registered through the public OrphanModel API, so the provider tests drive the public
// Begin/Apply engine over real tables: a nullable owned child, a RESTRICT pin onto it, a declared soft child and a
// child whose owner FK is NOT NULL.
internal static class ReplaceModel
{
    internal static readonly EntityDescriptor Parent = EntityDescriptor.Of(nameof(ReplaceParent), "orep_parents", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Name", "name", ColumnType.Text, false, false);
    internal static readonly EntityDescriptor Child = EntityDescriptor.Of(nameof(ReplaceChild), "orep_children", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .ForeignKey(new("fk_orep_child_parent", "parent_id", new("orep_parents", "id"), ReferentialAction.Restrict));
    internal static readonly EntityDescriptor Pin = EntityDescriptor.Of(nameof(ReplacePin), "orep_pins", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ChildId", "child_id", ColumnType.Uuid, false, false)
        .ForeignKey(new("fk_orep_pin_child", "child_id", new("orep_children", "id"), ReferentialAction.Restrict));
    internal static readonly EntityDescriptor Soft = EntityDescriptor.Of(nameof(ReplaceSoftChild), "orep_soft", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
        .ForeignKey(new("fk_orep_soft_parent", "parent_id", new("orep_parents", "id"), ReferentialAction.Restrict))
        .SoftDelete("DeletedAt", "deleted_at", "orep_soft");
    internal static readonly EntityDescriptor Strict = EntityDescriptor.Of(nameof(ReplaceStrictChild), "orep_strict", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, false, false)
        .ForeignKey(new("fk_orep_strict_parent", "parent_id", new("orep_parents", "id"), ReferentialAction.Restrict));

    private static readonly OrphanEntityBinding<ReplaceParent, Guid> ParentBinding = new(Parent,
        static row => [row.Id, row.Name], static row => row.Id);

    static ReplaceModel()
    {
        OrmMeta.Register<ReplaceParent>(Parent);
        OrmMeta.Register<ReplaceChild>(Child);
        OrmMeta.Register<ReplacePin>(Pin);
        OrmMeta.Register<ReplaceSoftChild>(Soft);
        OrmMeta.Register<ReplaceStrictChild>(Strict);
        OrphanModel.RegisterCollection(ParentBinding, new OrphanEntityBinding<ReplaceChild, Guid>(Child,
            static row => [row.Id, row.ParentId, row.Name], static row => row.Id), new("Children", "ParentId"));
        OrphanModel.RegisterCollection(ParentBinding, new OrphanEntityBinding<ReplaceSoftChild, Guid>(Soft,
            static row => [row.Id, row.ParentId, row.Name, row.DeletedAt], static row => row.Id),
            new("SoftChildren", "ParentId"));
        OrphanModel.RegisterCollection(ParentBinding, new OrphanEntityBinding<ReplaceStrictChild, Guid>(Strict,
            static row => [row.Id, row.ParentId], static row => row.Id), new("StrictChildren", "ParentId"));
    }

    internal static OwnedCollection<ReplaceParent, ReplaceChild, Guid, Guid> Children =>
        OrphanCollections.Relation<ReplaceParent, ReplaceChild, Guid, Guid>("Children");
    internal static OwnedCollection<ReplaceParent, ReplaceSoftChild, Guid, Guid> SoftChildren =>
        OrphanCollections.Relation<ReplaceParent, ReplaceSoftChild, Guid, Guid>("SoftChildren");
    internal static OwnedCollection<ReplaceParent, ReplaceStrictChild, Guid, Guid> StrictChildren =>
        OrphanCollections.Relation<ReplaceParent, ReplaceStrictChild, Guid, Guid>("StrictChildren");

    internal static Guid Key(int value) => new(value, 0, 0, new byte[8]);
    internal static OrphanOptions Options(OrphanMode mode) => new(new(20, 20, 20, 20, 400), mode);
    internal static OrphanReplacementRequest<ReplaceChild, Guid> Keep(params int[] keys) =>
        new(keys.Select(Key).ToArray(), OrphanPresentWrites<ReplaceChild>.None);
    internal static void Register() { }
}
