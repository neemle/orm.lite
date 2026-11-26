using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace OneToOneDemo;

internal static class DeclarationCases
{
    internal static void Run(Fixture f)
    {
        var parent = EntityDescriptor.Of("ManualParent", "manual_parent", true)
            .Property("Id", "id", ColumnType.Int32, false, true);
        var relation = new OneToOneDescriptor(new("ManualChild", "ParentId", "parent_id"),
            new("ManualParent", "id", "Parent", "Child"), new("fk_manual", "uq_manual", ReferentialAction.Restrict));
        var before = f.Counter.Count;
        Require(Child().OneToOne(relation with { Constraints = null! }), parent, OneToOneError.InvalidOwner);
        Require(Child().Index("uq_manual", true, false, "parent_id").OneToOne(relation), parent,
            OneToOneError.ConflictingConstraint);
        Require(Child().OneToOne(relation with { Owner = relation.Owner with { ForeignKeyProperty = "Missing" } }),
            parent, OneToOneError.UnmappedForeignKey);
        Results.Require(before == f.Counter.Count, "invalid manual declaration refuses without SQL");
        Results.Case("declarations", "malformed metadata|name collision|unmapped physical FK:zero SQL");
    }

    private static EntityDescriptor Child() => EntityDescriptor.Of("ManualChild", "manual_child", true)
        .Property("Id", "id", ColumnType.Int32, false, true)
        .Property("ParentId", "parent_id", ColumnType.Int32, true, false);

    private static void Require(EntityDescriptor child, EntityDescriptor parent, OneToOneError expected)
    {
        try { OrmSchema.Of("InvalidManual", child, parent).Baseline(); }
        catch (OneToOneDeclarationException error)
        { Results.Require(error.Error == expected, "exact manual declaration refusal"); return; }
        throw new InvalidOperationException("Invalid manual metadata was admitted");
    }
}
