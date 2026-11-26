using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class SdetForeignKeyOwnerTests
{
    [Theory(DisplayName = "[UC-003] an ordinary local FK does not impose a key requirement on an unrelated owner Id")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Local_reference_targets_the_existing_parent_key(bool key, bool nullable)
    {
        var owner = EntityDescriptor.Of("Child", "children", true)
            .Property("Id", "id", ColumnType.Uuid, nullable, key)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .ForeignKey(ForeignKeyPlans.Key());
        var schema = OrmSchema.Of("OwnerKeys", owner, ForeignKeyPlans.Parent());

        var table = Assert.Single(schema.Baseline().OfType<CreateTable>(), t => t.Table == "children");
        var constraint = Assert.IsType<ForeignKeyDef>(Assert.Single(table.Constraints!));

        Assert.Equal("fk_child_parent", constraint.Constraint);
        Assert.Equal("parent_id", Assert.IsType<ColumnRef>(Assert.Single(constraint.Columns)).Column);
        Assert.Equal("parents", constraint.ReferencedTable);
        Assert.Equal("id", Assert.IsType<ColumnRef>(Assert.Single(constraint.ReferencedColumns)).Column);
        Assert.Equal(ReferentialAction.Restrict, constraint.OnDelete);
        Assert.Null(constraint.OnUpdate);
    }

    [Theory(DisplayName = "[SEC][UC-003] the referenced target requires an existing nonnullable single primary key")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Missing_or_nullable_target_key_refuses_before_baseline_publication(bool key, bool nullable)
    {
        var parent = EntityDescriptor.Of("Parent", "parents", true)
            .Property("Id", "id", ColumnType.Uuid, nullable, key);
        var schema = OrmSchema.Of("TargetKeys", ForeignKeyPlans.Child().ForeignKey(ForeignKeyPlans.Key()), parent);

        var error = Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline());

        Assert.Equal(ForeignKeyDeclarationError.UnsupportedKey, error.Error);
    }
}
