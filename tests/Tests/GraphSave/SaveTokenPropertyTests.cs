using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveTokenPropertyTests
{
    [Fact(DisplayName = "[UC-006][AC-112-02][SEC-112-01] generated save tokens are read-only properties "
        + "with exact entity types")]
    public void A_save_property_adds_typed_tokens_without_a_public_construction_route()
    {
        var (diagnostics, _, output) = Run(GeneratedSaveConsumerTests.Source);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var orm = model.Class("SaveParentOrm");
        var property = orm.GetProperty("SaveRelations", BindingFlags.Public | BindingFlags.Static)!;
        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
        Assert.True(property.GetMethod!.IsStatic);
        Assert.Empty(property.GetMethod.GetParameters());
        var tokens = property.GetValue(null)!;
        Assert.True(property.PropertyType.IsSealed);
        Assert.Empty(property.PropertyType.GetConstructors());
        var child = property.PropertyType.GetProperty("Children")!;
        Assert.Null(child.SetMethod);
        Assert.Equal(typeof(SaveRelation<,>), child.PropertyType.GetGenericTypeDefinition());
        Assert.Equal(["Fixture.SaveParent", "Fixture.SaveChild"],
            child.PropertyType.GenericTypeArguments.Select(type => type.FullName));
        Assert.Same(tokens, property.GetValue(null));
        var value = child.GetValue(tokens);
        Assert.NotNull(value);
        Assert.Empty(value.GetType().GetConstructors());
    }
}
