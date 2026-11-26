using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class SdetForeignKeyPlanTests
{
    [Fact(DisplayName = "[UC-003] a diamond DAG has ordinal ties and discovery-order-independent constraints")]
    public void Diamond_order_and_constraint_identity_do_not_depend_on_discovery_order()
    {
        var root = ForeignKeyPlans.Parent("root");
        var left = Child("left", "root", "fk_left_root");
        var right = Child("right", "root", "fk_right_root");
        var bottom = Child("bottom", "left", "fk_bottom_left")
            .Property("RightId", "right_id", ColumnType.Uuid, true, false)
            .ForeignKey(new("fk_bottom_right", "right_id", new("right", "id"), ReferentialAction.NoAction));
        var first = OrmSchema.Of("Diamond", bottom, right, root, left).Baseline();
        var second = OrmSchema.Of("Diamond", left, root, right, bottom).Baseline();
        Assert.Equal(new[] { "root", "left", "right", "bottom" }, first.OfType<CreateTable>().Select(t => t.Table));
        Assert.Equal(GeneratedModel.Render(first), GeneratedModel.Render(second));
        Assert.Equal(new[] { "fk_bottom_left", "fk_bottom_right" }, first.OfType<CreateTable>().Last()
            .Constraints!.Cast<ForeignKeyDef>().Select(k => k.Constraint));
    }

    [Theory(DisplayName = "[SEC][UC-003] invalid link roles or unresolved navigation refuse the complete plan")]
    [InlineData("Tags", (LinkForeignKeyEnd)99, ForeignKeyDeclarationError.UnsupportedLink)]
    [InlineData("Missing", LinkForeignKeyEnd.Source, ForeignKeyDeclarationError.UnknownRelation)]
    public void Invalid_manual_link_role_never_retargets_an_endpoint(string navigation, LinkForeignKeyEnd end,
        ForeignKeyDeclarationError expected)
    {
        var owner = ForeignKeyPlans.Parent().Link("Tags", "parent_tags", "parent_id", "tag_id", "tags", "id",
                null, true)
            .LinkForeignKey(navigation, end, new("fk_role", ReferentialAction.Restrict));
        var error = Assert.Throws<ForeignKeyDeclarationException>(() =>
            OrmSchema.Of("Roles", owner, ForeignKeyPlans.Parent("tags")).Baseline());
        Assert.Equal(expected, error.Error);
        Assert.Equal(navigation, error.Navigation);
    }

    [Fact(DisplayName = "[UC-003] null public metadata is refused before being added to a descriptor")]
    public void Null_definitions_do_not_poison_later_valid_schema_planning()
    {
        var child = ForeignKeyPlans.Child();
        Assert.Throws<ArgumentNullException>(() => child.ForeignKey(null!));
        Assert.Throws<ArgumentNullException>(() => child.LinkForeignKey("Tags", LinkForeignKeyEnd.Source, null!));
        Assert.Empty(child.ForeignKeys);
        child.ForeignKey(ForeignKeyPlans.Key());
        Assert.Single(OrmSchema.Of("Valid", child, ForeignKeyPlans.Parent()).Baseline().OfType<CreateTable>()
            .Single(table => table.Table == "children").Constraints!);
    }

    private static EntityDescriptor Child(string table, string target, string name) =>
        EntityDescriptor.Of(table, table, true).Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .ForeignKey(new(name, "parent_id", new(target, "id"), ReferentialAction.Restrict));
}
