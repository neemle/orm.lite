using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.OneToOne;

public sealed class OneToOneManualTests
{
    private static EntityDescriptor Parent(bool managed = true) => EntityDescriptor.Of("P", "sdet_p", managed)
        .Property("Id", "id", ColumnType.Int32, false, true);

    private static EntityDescriptor Child() => EntityDescriptor.Of("C", "sdet_c", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Int32, true, false);

    private static OneToOneDescriptor Pair() => new(new("C", "ParentId", "parent_id"),
        new("P", "id", "Parent", "Child"), new("fk_sdet", "uq_sdet", ReferentialAction.Restrict));

    [Fact(DisplayName = "[UC-005] equivalent manual pair and FK declarations deduplicate without reconcile drift")]
    public void Equivalent_pair_is_one_constraint_identity()
    {
        var child = Child(); var pair = Pair();
        var plain = OrmSchema.Of("SdetManual", child, Parent()).IndexSetHash;
        child.OneToOne(pair).OneToOne(pair with { Owner = pair.Owner with { } });
        child.ForeignKey(new("fk_sdet", "parent_id", new("sdet_p", "id"), ReferentialAction.Restrict));
        var schema = OrmSchema.Of("SdetManual", child, Parent());
        var tables = schema.Baseline().OfType<CreateTable>().ToArray();
        Assert.Single(schema.Baseline().OfType<CreateIndex>());
        Assert.Single(tables.Single(t => t.Table == "sdet_c").Constraints!);
        Assert.Equal(plain, schema.IndexSetHash);
        Assert.Empty(schema.Indexes());
    }

    [Theory(DisplayName = "[UC-005][SEC] manual endpoint and key mismatch refuses before schema publication")]
    [InlineData("unmanaged", OneToOneError.UnsupportedSchema)]
    [InlineData("view", OneToOneError.UnsupportedSchema)]
    [InlineData("external", OneToOneError.UnsupportedSchema)]
    [InlineData("key", OneToOneError.UnsupportedKey)]
    [InlineData("property", OneToOneError.UnmappedForeignKey)]
    [InlineData("owner", OneToOneError.InvalidOwner)]
    public void Manual_endpoints_preserve_the_declared_pair(string defect, OneToOneError expected)
    {
        var parent = Parent(defect != "unmanaged"); var pair = Pair();
        if (defect == "view") parent.View();
        if (defect == "external") pair = pair with { Target = pair.Target with { Entity = "Other" } };
        if (defect == "key") pair = pair with { Target = pair.Target with { KeyColumn = "not_pk" } };
        if (defect == "property") pair = pair with { Owner = pair.Owner with { ForeignKeyProperty = "Absent" } };
        if (defect == "owner") pair = pair with { Owner = pair.Owner with { Entity = "Other" } };
        var schema = OrmSchema.Of("SdetInvalid", Child().OneToOne(pair), parent);
        Assert.Equal(expected, Assert.Throws<OneToOneDeclarationException>(() => schema.Baseline()).Error);
    }

    [Fact(DisplayName = "[UC-005][SEC] decimal precision mismatch cannot weaken the manual owning FK")]
    public void Decimal_shape_is_exact()
    {
        var parent = EntityDescriptor.Of("P", "sdet_p", true)
            .Property("Id", "id", ColumnType.Decimal, false, true, 12, 2);
        var child = EntityDescriptor.Of("C", "sdet_c", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("ParentId", "parent_id", ColumnType.Decimal, true, false, 12, 3).OneToOne(Pair());
        Assert.Equal(OneToOneError.IncompatibleKey, Assert.Throws<OneToOneDeclarationException>(
            () => OrmSchema.Of("SdetPrecision", child, parent).Baseline()).Error);
    }
}
