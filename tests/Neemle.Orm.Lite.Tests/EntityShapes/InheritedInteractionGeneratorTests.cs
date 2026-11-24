using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.InheritedShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class InheritedInteractionGeneratorTests
{
    [Fact(DisplayName = "B21 [Column], [NotAColumn] and an [Index] on a property are read from the base class")]
    public void Base_attributes_on_properties_are_honoured()
    {
        var (diagnostics, _, output) = Run(Attributed);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "Code", "Title"], Columns(model, "Doc"));
        var descriptor = model.Descriptor("Doc");
        Assert.Equal(["ident", "code", "title"], descriptor.Columns.Select(c => c.Column).ToArray());
        Assert.Equal("ident", Assert.Single(descriptor.Columns, c => c.Key).Column);
        Assert.Contains(descriptor.Indexes, i => i.Columns.Count == 1 && i.Columns[0] == "code");
    }

    [Fact(DisplayName = "B21 an inherited property of an unmapped type gives ORMLITE001 as a declared one does")]
    public void An_inherited_unmapped_type_is_reported()
    {
        var (diagnostics, _, output) = Run(UnmappedType);

        var report = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", report.Id);
        Assert.Contains("Doc.RowVersion", report.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("RowVersion", SpanText(report));
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "B21 an inherited key by name plus a declared [Key] is the composite-key refusal")]
    public void A_second_key_on_the_derived_class_is_refused()
    {
        var (diagnostics, _, output) = Run(SecondKey);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE022", refusal.Id);
        Assert.Contains("'Doc'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Id (by name)", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
        Assert.DoesNotContain("GetAsync", Methods(GeneratedModel.Of(output), "DocOrm"));
    }

    [Fact(DisplayName = "B21 a soft-delete entity whose base lacks the column gives the usual ORMLITE002")]
    public void A_base_without_the_soft_column_is_refused_as_before()
    {
        var (diagnostics, _, output) = Run(SoftWithoutColumn);

        Assert.Equal("ORMLITE002", Assert.Single(diagnostics).Id);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "B21 an inherited collection and an inherited relation get their loaders")]
    public void Inherited_navigations_get_loaders()
    {
        var (diagnostics, _, output) = Run(Loaders);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var methods = Methods(GeneratedModel.Of(output), "ParentOrm");
        Assert.Contains("LoadKidsAsync", methods);
        Assert.Contains("LoadFavAsync", methods);
    }
}
