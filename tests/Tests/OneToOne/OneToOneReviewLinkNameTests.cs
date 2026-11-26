using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.OneToOne;

public sealed class OneToOneReviewLinkNameTests
{
    private static OrmSchema Schema(string linkName)
    {
        var parent = EntityDescriptor.Of("R1Parent", "r1_parent", true)
            .Property("Id", "id", ColumnType.Uuid, false, true);
        var tag = EntityDescriptor.Of("R1Tag", "r1_tag", true)
            .Property("Id", "id", ColumnType.Uuid, false, true);
        var owner = EntityDescriptor.Of("R1Owner", "r1_owner", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
            .OneToOne(new(new("R1Owner", "ParentId", "parent_id"),
                new("R1Parent", "id", "Parent", "Owner"), new("fk_oto", "uq_shared", ReferentialAction.Restrict)))
            .Link("Tags", "r1_join", "owner_id", "tag_id", "r1_tag", "id", null, true)
            .LinkForeignKey("Tags", LinkForeignKeyEnd.Source, new(linkName, ReferentialAction.Restrict));
        return OrmSchema.Of("R1Review", owner, parent, tag);
    }

    [Theory(DisplayName = "[UC-005][SEC][R1] link-FK and one-to-one unique names share one insensitive inventory")]
    [InlineData("uq_shared")]
    [InlineData("UQ_SHARED")]
    public void Cross_kind_name_collision_refuses_the_complete_baseline(string linkName)
    {
        IReadOnlyList<Node>? published = null;
        var error = Assert.Throws<OneToOneDeclarationException>(() => published = Schema(linkName).Baseline());
        Assert.Equal(OneToOneError.ConflictingConstraint, error.Error);
        Assert.Null(published);
    }

    [Fact(DisplayName = "[UC-005][R1] distinct pure-link FK and one-to-one unique keep exact physical nodes")]
    public void Distinct_names_preserve_the_link_and_unique()
    {
        var nodes = Schema("fk_link_distinct").Baseline();
        Assert.Equal(4, nodes.OfType<CreateTable>().Count());
        var owner = nodes.OfType<CreateTable>().Single(t => t.Table == "r1_owner");
        var ownerKey = Assert.IsType<ForeignKeyDef>(Assert.Single(owner.Constraints!));
        Assert.Equal("fk_oto", ownerKey.Constraint);
        Assert.Equal("r1_parent", ownerKey.ReferencedTable);
        var join = nodes.OfType<CreateTable>().Single(t => t.Table == "r1_join");
        var link = Assert.Single(join.Constraints!.OfType<ForeignKeyDef>());
        Assert.Equal("fk_link_distinct", link.Constraint);
        Assert.Equal("r1_owner", link.ReferencedTable);
        Assert.Equal("owner_id", Assert.IsType<ColumnRef>(Assert.Single(link.Columns)).Column);
        var unique = Assert.IsType<CreateIndex>(nodes.Last());
        Assert.Equal("uq_shared", unique.Index);
        Assert.True(unique.Unique);
        Assert.Null(unique.Where);
        Assert.Equal("parent_id", Assert.IsType<ColumnRef>(Assert.Single(unique.Terms)).Column);
    }
}
