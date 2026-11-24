using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class RecordEntityGeneratorTests
{
    private const string TableRecord = """
        [Table("a")]
        public record A
        {
            [Key] public Guid Id { get; init; }
            public string Title { get; init; } = "";
        }
        """;

    private const string ViewRecord = """
        [View("v")]
        public record V
        {
            public Guid Id { get; init; }
            public string Title { get; init; } = "";
        }
        """;

    private const string RecordStruct = """
        [Table("a")]
        public record struct A
        {
            public Guid Id { get; set; }
        }
        """;

    private const string PlainStruct = """
        [Table("a")]
        public struct A
        {
            public Guid Id { get; set; }
        }
        """;

    [Fact(DisplayName = "AC9 a [Table] record class generates its repository and builds through the initializer")]
    public void A_table_record_class_is_an_entity()
    {
        var (diagnostics, generated, model) = Built(TableRecord);

        Assert.Empty(diagnostics);
        Assert.Equal(
            ["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "UpdateAsync", "Values"],
            Methods(model, "AOrm"));
        Assert.Contains(
            "new global::Fixture.A() { Id = r.GetGuid(o0), Title = r.GetString(o1) }",
            MapOf(generated, "A"),
            StringComparison.Ordinal);
        Assert.Equal(["Id", "Title"], Columns(model, "A"));
    }

    [Fact(DisplayName = "AC9 a [View] record class generates its read-only repository")]
    public void A_view_record_class_is_an_entity()
    {
        var (diagnostics, _, model) = Built(ViewRecord);

        Assert.Empty(diagnostics);
        Assert.True(model.Descriptor("V").IsView);
        Assert.Equal(["Id", "Title"], Columns(model, "V"));
    }

    [Fact(DisplayName = "AC9 a positional record gets T and its repository exists with a throwing Map")]
    public void A_positional_record_is_refused_without_a_cascade()
    {
        var (diagnostics, _, output) = Run("[Table(\"a\")] public record A([property: Key] Guid Id, string Title);\n");

        Assert.Equal(TypeId, Assert.Single(diagnostics).Id);
        var model = GeneratedModel.Of(output);
        Assert.True(model.Has("AOrm"));
        Assert.Contains("A cannot be built from a row", MapThrows(model, "A").Message, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName = "AC9 a record struct and a struct stay unmatched: the compiler's own error is the only "
        + "error and a sibling is generated")]
    [InlineData(RecordStruct)]
    [InlineData(PlainStruct)]
    public void A_struct_entity_is_not_generated(string source)
    {
        var (diagnostics, generated, output) = Run(source + Sibling);

        Assert.Empty(diagnostics);
        var errors = CompileErrors(output);
        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal("CS0592", error.Id));
        Assert.Contains("class SibOrm", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("class AOrm", generated, StringComparison.Ordinal);
    }
}
