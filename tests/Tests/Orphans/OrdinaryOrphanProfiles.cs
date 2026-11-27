using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class OrdinaryOrphanProfiles
{
    internal static readonly EntityDescriptor Scalars = EntityDescriptor.Of(nameof(OrdinaryScalarEntity), "ordinary_scalars", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Text", "text", ColumnType.Text, false, false)
        .Property("Enabled", "enabled", ColumnType.Bool, false, false)
        .Property("Count", "count", ColumnType.Int32, false, false)
        .Property("Total", "total", ColumnType.Int64, false, false)
        .Property("Number", "number", ColumnType.Double, false, false)
        .Property("ObservedAt", "observed_at", ColumnType.Timestamp, true, false)
        .ForeignKey(new("fk_ordinary_scalar_parent", "parent_id", new("guard_parents", "id"), ReferentialAction.Restrict));

    internal static readonly EntityDescriptor Soft = EntityDescriptor.Of(nameof(OrdinarySoftEntity), "ordinary_soft", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
        .ForeignKey(new("fk_ordinary_soft_parent", "parent_id", new("guard_parents", "id"), ReferentialAction.Restrict))
        .SoftDelete("DeletedAt", "deleted_at", "ordinary_soft")
        .Index("ix_ordinary_soft_name", true, true, "name");

    static OrdinaryOrphanProfiles()
    {
        _ = GuardNativeModel.Request();
        OrmMeta.Register<OrdinaryScalarEntity>(Scalars);
        OrmMeta.Register<OrdinarySoftEntity>(Soft);
        OrphanModel.RegisterEntity(new OrphanEntityBinding<OrdinaryScalarEntity, Guid>(Scalars,
            static row => [row.Id, row.ParentId, row.Text, row.Enabled, row.Count, row.Total, row.Number, row.ObservedAt],
            static row => row.Id));
        OrphanModel.RegisterEntity(new OrphanEntityBinding<OrdinarySoftEntity, Guid>(Soft,
            static row => [row.Id, row.ParentId, row.Name, row.DeletedAt], static row => row.Id));
    }

    internal static OrphanGuardRequest Request(bool soft) =>
        new(GuardNativeModel.Parent, soft ? Soft : Scalars, "ParentId", GuardNativeModel.Key(1),
            new(10, 10, 10, 10, 500));
}
