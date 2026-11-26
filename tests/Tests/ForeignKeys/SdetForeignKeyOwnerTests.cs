using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class SdetForeignKeyOwnerTests
{
    [Theory(DisplayName = "[SEC][UC-003] a real FK owner requires an existing nonnullable single primary key")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Unsupported_real_owner_key_withholds_the_complete_plan(bool key, bool nullable)
    {
        var owner = EntityDescriptor.Of("Child", "children", true)
            .Property("Id", "id", ColumnType.Uuid, nullable, key)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .ForeignKey(ForeignKeyPlans.Key());
        var schema = OrmSchema.Of("OwnerKeys", owner, ForeignKeyPlans.Parent());

        var error = Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline());

        Assert.Equal(ForeignKeyDeclarationError.UnsupportedKey, error.Error);
    }
}
