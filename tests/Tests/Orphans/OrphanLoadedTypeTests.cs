using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanLoadedTypeTests
{
    private const string Source = """
        [Table("u1_loaded_parent")]
        public sealed class LoadedParent
        {
            [Key] public Guid Id { get; set; }
            [Reference(nameof(LoadedChild.ParentId))]
            public System.Collections.Generic.List<LoadedChild> Children { get; set; } = new();
        }
        [Table("u1_loaded_child")]
        public sealed class LoadedChild
        {
            [Key] public Guid Id { get; set; }
            public Guid ParentId { get; set; }
            [ForeignKey("fk_u1_loaded", ReferentialAction.Cascade)]
            public LoadedParent? Parent { get; set; }
        }
        """;

    [Fact(DisplayName = "[UC-007][SEC-113-01] equal qualified names in separate load contexts retain actual Type ownership")]
    public void Separate_emissions_of_the_same_compilation_keep_isolated_bindings()
    {
        var (diagnostics, _, compilation) = Run(Source);
        Assert.Empty(diagnostics);
        var first = GeneratedModel.Of(compilation);
        var second = GeneratedModel.Of(compilation);
        first.RunModuleInitializer(); second.RunModuleInitializer();
        var parentA = Entity(first, "LoadedParent"); var childA = Entity(first, "LoadedChild");
        var parentB = Entity(second, "LoadedParent"); var childB = Entity(second, "LoadedChild");
        Assert.NotSame(parentA, parentB);
        Assert.Equal(parentA.AssemblyQualifiedName, parentB.AssemblyQualifiedName);
        Assert.Same(first.Descriptor("LoadedParent"), OrmMeta.ReadDescriptor(parentA));
        Assert.Same(second.Descriptor("LoadedParent"), OrmMeta.ReadDescriptor(parentB));
        var a = OrphanModel.Resolve(parentA, childA, "Children");
        var b = OrphanModel.Resolve(parentB, childB, "Children");
        Assert.NotSame(a, b);
        Assert.Equal(parentA, a.Parent.EntityType); Assert.Equal(childA, a.Child.EntityType);
        Assert.Equal(parentB, b.Parent.EntityType); Assert.Equal(childB, b.Child.EntityType);
        a.ValidateCurrent(); b.ValidateCurrent();
        var refused = Assert.Throws<OrphanException>(() => OrphanModel.Resolve(parentA, childB, "Children"));
        Assert.Equal(OrphanError.UnsupportedMapping, refused.Error);
        Assert.Equal(OrphanOutcome.NotStarted, refused.Outcome);
    }

    private static Type Entity(GeneratedModel model, string name) =>
        model.Class(name + "Orm").Assembly.GetType("Fixture." + name, true)!;
}
