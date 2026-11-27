using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGeneratedBindingTests
{
    [Fact(DisplayName = "[UC-007][AC-113-04][SEC-113-01] generated nullable owned FK resolves its exact typed collection")]
    public async Task A_generated_nullable_owner_fk_has_an_exact_registered_relation()
    {
        var assembly = await OrphanGeneratedFixture.Compiled.Value;
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        var consumer = assembly.GetType("OrphanConsumerFixture.Consumer", true)!;
        var token = consumer.GetMethod("Relation")!.Invoke(null, null);
        var binding = Assert.IsAssignableFrom<IOrphanCollection>(token);
        Assert.Equal("orphan_parent", binding.Parent.Metadata.Copy().Table);
        Assert.Equal("orphan_child", binding.Child.Metadata.Copy().Table);
        Assert.Equal("parent_id", binding.Owner.Column);
        Assert.True(binding.Owner.Nullable);
        Assert.Equal("ParentId", binding.Shape.OwnerForeignKey);
        var child = consumer.GetMethod("NewChild")!.Invoke(null, null)!;
        Assert.Equal(new Guid(113, 0, 0, new byte[8]), binding.Child.Key(child));
        Assert.Equal([new Guid(113, 0, 0, new byte[8]), new Guid(7, 0, 0, new byte[8]), "child"],
            binding.Child.Values(child));
        Assert.Empty(token!.GetType().GetConstructors());
        Assert.True(token.GetType().IsSealed);
    }
}
