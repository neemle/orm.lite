using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class ForeignKeyPlans
{
    internal static EntityDescriptor Parent(string table = "parents") => EntityDescriptor.Of("Parent", table, true)
        .Property("Id", "id", ColumnType.Uuid, false, true);

    internal static EntityDescriptor Child(bool nullable = false) => EntityDescriptor.Of("Child", "children", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, nullable, false);

    internal static ForeignKeyDescriptor Key(string name = "fk_child_parent",
        ReferentialAction action = ReferentialAction.Restrict) => new(name, "parent_id", new("parents", "id"), action);

    [Theory(DisplayName = "[UC-003] fresh baseline keeps parent first and exact action")]
    [InlineData(ReferentialAction.NoAction)]
    [InlineData(ReferentialAction.Restrict)]
    [InlineData(ReferentialAction.Cascade)]
    [InlineData(ReferentialAction.SetNull)]
    public void Physical_constraints_are_inline_parent_first(ReferentialAction action)
    {
        var schema = OrmSchema.Of("Plan", Child(action == ReferentialAction.SetNull).ForeignKey(Key(action: action)),
            Parent());
        var plan = schema.Baseline().Cast<CreateTable>().ToArray();
        Assert.Equal(["parents", "children"], plan.Select(t => t.Table));
        var key = Assert.IsType<ForeignKeyDef>(Assert.Single(plan[1].Constraints!));
        Assert.Equal("fk_child_parent", key.Constraint);
        Assert.Equal("parent_id", Assert.IsType<ColumnRef>(Assert.Single(key.Columns)).Column);
        Assert.Equal("parents", key.ReferencedTable);
        Assert.Equal("id", Assert.IsType<ColumnRef>(Assert.Single(key.ReferencedColumns)).Column);
        Assert.Equal(action, key.OnDelete);
        Assert.Null(key.OnUpdate);
    }

    [Fact(DisplayName = "[UC-003] repeated identical physical intent emits once, without mutating metadata")]
    public void Equal_intent_deduplicates_and_generation_repeats()
    {
        var child = Child().ForeignKey(Key()).ForeignKey(Key());
        var schema = OrmSchema.Of("Duplicate", child, Parent());
        var first = GeneratedModel.Render(schema.Baseline());
        Assert.Single(schema.Baseline().Cast<CreateTable>().Last().Constraints!);
        Assert.Equal(first, GeneratedModel.Render(schema.Baseline()));
        Assert.Equal(2, child.ForeignKeys.Count);
    }

    [Fact(DisplayName = "[UC-003] no opt-ins retain insertion order, null constraints and baseline identities")]
    public void Unconfigured_path_keeps_original_nodes_and_versions()
    {
        var schema = OrmSchema.Of("Old", Child(), Parent());
        var plan = schema.Baseline().Cast<CreateTable>().ToArray();
        Assert.Equal(["children", "parents"], plan.Select(t => t.Table));
        Assert.All(plan, t => Assert.Null(t.Constraints));
        Assert.Equal("0000_baseline_Old", schema.Contributions()[0].Version);
        Assert.Equal("0001_orm_indexes_Old", schema.Contributions()[1].Version);
    }

    [Fact(DisplayName = "[SEC][UC-003] conflicting duplicate intent refuses the complete plan")]
    public void Conflicting_intent_never_returns_a_partial_plan()
    {
        var child = Child().ForeignKey(Key()).ForeignKey(Key(action: ReferentialAction.Cascade));
        var error = Assert.Throws<ForeignKeyDeclarationException>(
            () => OrmSchema.Of("Bad", Parent(), child).Baseline());
        Assert.Equal(ForeignKeyDeclarationError.ConflictingDeclaration, error.Error);
    }

    [Fact(DisplayName = "[SEC][UC-003] case-insensitive constraint names are schema-wide")]
    public void Duplicate_names_are_not_silently_selected()
    {
        var parent = Parent().Property("AncestorId", "ancestor_id", ColumnType.Uuid, true, false)
            .ForeignKey(new("FK_CHILD_PARENT", "ancestor_id", new("ancestors", "id"), ReferentialAction.NoAction));
        var schema = OrmSchema.Of("Names", parent, Child().ForeignKey(Key()), Parent("ancestors"));
        var error = Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline());
        Assert.Equal(ForeignKeyDeclarationError.DuplicateName, error.Error);
    }

    [Fact(DisplayName = "[UC-003] pure Guid join ends are independent, inline and after both parents")]
    public void Pure_link_endpoints_are_owned_by_the_join()
    {
        var parent = Parent().Link("Tags", "parent_tags", "parent_id", "tag_id", "tags", "id", null, true)
            .LinkForeignKey("Tags", LinkForeignKeyEnd.Source, new("fk_link_parent", ReferentialAction.Restrict))
            .LinkForeignKey("Tags", LinkForeignKeyEnd.Target, new("fk_link_tag", ReferentialAction.Cascade));
        var plan = OrmSchema.Of("Link", parent, Parent("tags")).Baseline().Cast<CreateTable>().ToArray();
        Assert.Equal(["parents", "tags", "parent_tags"], plan.Select(t => t.Table));
        var constraints = plan[2].Constraints!.Cast<ForeignKeyDef>().ToArray();
        Assert.Equal(["fk_link_parent", "fk_link_tag"], constraints.Select(k => k.Constraint));
        Assert.Equal([ReferentialAction.Restrict, ReferentialAction.Cascade],
            constraints.Select(k => k.OnDelete!.Value));
        Assert.All(plan.Take(2), t => Assert.Empty(t.Constraints!));
    }
}
