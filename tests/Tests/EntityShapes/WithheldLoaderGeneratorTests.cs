using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.MemberShapeSources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class WithheldLoaderGeneratorTests
{
    private const string SelfReferencing = """
        [Table("nodes")]
        public class Node
        {
            [Key] public Guid Id { get; set; }
            public Guid NodeId { private get; set; }
            public System.Collections.Generic.List<Node> Children { get; set; } = new();
        }
        """;

    [Theory(
        DisplayName = "AC20 a child inverse key the generated code cannot use withholds the parent's loader and "
        + "nothing else")]
    [InlineData("public Guid PId { private get; set; }")]
    [InlineData("public Guid PId { protected get; set; }")]
    [InlineData("public Guid PId { private protected get; set; }")]
    [InlineData("public Guid PId { get; private set; }")]
    [InlineData("public Guid PId { get; protected set; }")]
    public void An_excluded_inverse_key_withholds_the_loader(string childKey)
    {
        var (diagnostics, _, output) = Run(ParentOf(childKey));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Contains("'C.PId'", refusal.GetMessage(), StringComparison.Ordinal);
        var model = GeneratedModel.Of(output);
        Assert.DoesNotContain("LoadKidsAsync", Methods(model, "POrm"));
        Assert.Contains("LoadOthersAsync", Methods(model, "POrm"));
    }

    [Fact(DisplayName = "AC20 a named inverse key is withheld the same way")]
    public void A_named_inverse_key_is_withheld()
    {
        var (diagnostics, _, output) = Run(
            ParentOf("public Guid Owner { private get; set; }", "[Reference(\"Owner\")]"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        var methods = Methods(GeneratedModel.Of(output), "POrm");
        Assert.DoesNotContain("LoadKidsAsync", methods);
        Assert.Contains("LoadOthersAsync", methods);
    }

    [Theory(DisplayName = "AC20 a child key that stays readable keeps the loader byte for byte")]
    [InlineData("public Guid PId { get; init; }")]
    [InlineData("public Guid PId { internal get; set; }")]
    [InlineData("public Guid PId { get; internal set; }")]
    public void A_readable_inverse_key_keeps_the_loader(string childKey)
    {
        var (diagnostics, generated, _) = Built(ParentOf(childKey));
        var (_, control, _) = Run(ParentOf("public Guid PId { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Equal(LoaderOf(control, "LoadKidsAsync"), LoaderOf(generated, "LoadKidsAsync"));
    }

    // ORML-17 (B22) changes three cases that this class used to pin as "keeps the loader byte for byte": an inverse
    // key that is get-only, a field or [NotAColumn] is no mapped column, so the loader's Where(c => c.PId == ...)
    // throws InvalidOperationException (EntityDescriptor.ColumnOf) when it runs. The loader is now withheld with
    // ORMLITE026 naming the collection and the key. The mapped cases stay in the theory above.
    [Theory(
        DisplayName = "ORML-17 an inverse key that is no mapped column withholds the loader and says why")]
    [InlineData("public Guid PId { get; }")]
    [InlineData("public Guid PId;")]
    [InlineData("[NotAColumn] public Guid PId { get; set; }")]
    [InlineData("public Guid OtherId { get; set; }")]
    public void An_inverse_key_that_is_no_column_withholds_the_loader(string childKey)
    {
        var (diagnostics, _, output) = Run(ParentOf(childKey));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE026", refusal.Id);
        Assert.Contains("'P.Kids'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'PId'", refusal.GetMessage(), StringComparison.Ordinal);
        var methods = Methods(GeneratedModel.Of(output), "POrm");
        Assert.DoesNotContain("LoadKidsAsync", methods);
        Assert.Contains("LoadOthersAsync", methods);
    }

    [Fact(
        DisplayName = "AC20 an inverse key of an unmapped type gets ORMLITE001 only and the loader text is "
        + "unchanged")]
    public void An_unmapped_inverse_key_keeps_the_loader_text()
    {
        var (diagnostics, generated, _) = Run(ParentOf("public System.TimeSpan PId { get; set; }"));
        var (_, control, _) = Run(ParentOf("public Guid PId { get; set; }"));

        Assert.Equal("ORMLITE001", Assert.Single(diagnostics).Id);
        Assert.Equal(LoaderOf(control, "LoadKidsAsync"), LoaderOf(generated, "LoadKidsAsync"));
    }

    [Fact(DisplayName = "AC20 a self-referencing entity with an excluded inverse key loses its children loader")]
    public void A_self_referencing_entity_loses_the_loader()
    {
        var (diagnostics, _, model) = Built(SelfReferencing);

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.DoesNotContain("LoadChildrenAsync", Methods(model, "NodeOrm"));
    }
}
