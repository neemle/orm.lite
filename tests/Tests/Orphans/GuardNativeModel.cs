using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class GuardNativeModel
{
    internal static readonly EntityDescriptor Parent = EntityDescriptor.Of(nameof(GuardNativeParent), "guard_parents", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Name", "name", ColumnType.Text, false, false);
    internal static readonly EntityDescriptor Child = EntityDescriptor.Of(nameof(GuardNativeChild), "guard_children", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .ForeignKey(new("fk_guard_owner", "parent_id", new("guard_parents", "id"), ReferentialAction.Restrict));

    static GuardNativeModel()
    {
        OrmMeta.Register<GuardNativeParent>(Parent); OrmMeta.Register<GuardNativeChild>(Child);
        OrphanModel.RegisterEntity(new OrphanEntityBinding<GuardNativeParent, Guid>(Parent,
            static row => [row.Id, row.Name], static row => row.Id));
        OrphanModel.RegisterEntity(new OrphanEntityBinding<GuardNativeChild, Guid>(Child,
            static row => [row.Id, row.ParentId, row.Name], static row => row.Id));
    }

    internal static Guid Key(int value) => new(value, 0, 0, new byte[8]);
    internal static OrphanGuardRequest Request(int parent = 1, int observed = 10, int statements = 500) =>
        new(Parent, Child, "ParentId", Key(parent), new(observed, 10, 10, 10, statements));
}
