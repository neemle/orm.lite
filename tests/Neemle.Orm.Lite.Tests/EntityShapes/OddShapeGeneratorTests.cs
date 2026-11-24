using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class OddShapeGeneratorTests
{
    private const string Indexer = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string Title { get; set; } = "";
            public string this[int i] { get => ""; set { } }
        }
        """;

    private const string ErrorType = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public Missing Thing { get; set; }
        }
        """;

    private const string UnresolvedBase = """
        [Table("a")]
        public class A : MissingBase
        {
            [Key] public Guid Id { get; set; }
        }
        """;

    private const string Partial = """
        [Table("a")]
        public partial class A
        {
            [Key] public Guid Id { get; set; }
        }

        public partial class A
        {
            public string Title { get; set; } = "";
        }
        """;

    private const string PartialWithConstructor = """
        [Table("a")]
        public partial class A
        {
            [Key] public Guid Id { get; set; }
        }

        public partial class A
        {
            private A() { }
        }
        """;

    private const string Nested = """
        public class Outer
        {
            [Table("n")]
            public class Inner
            {
                [Key] public Guid Id { get; set; }
            }
        }
        """;

    private const string Generic = """
        [Table("g")]
        public class G<T>
        {
            [Key] public Guid Id { get; set; }
            public T? Value { get; set; }
        }
        """;

    private const string Verbatim = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { get; set; }
            public string @class { get; set; } = "";
        }
        """;

    [Theory(DisplayName = "AC18 an odd entity shape never makes the generator throw")]
    [InlineData(Indexer)]
    [InlineData(ErrorType)]
    [InlineData(UnresolvedBase)]
    [InlineData(Partial)]
    [InlineData(PartialWithConstructor)]
    [InlineData(Nested)]
    [InlineData(Generic)]
    [InlineData(Verbatim)]
    public void The_generator_does_not_throw_for_an_odd_shape(string source)
    {
        var (diagnostics, generated, _) = Run(source);

        Assert.DoesNotContain(diagnostics, d => d.Id is "CS8785" or "AD0001");
        Assert.NotEmpty(generated);
    }

    [Fact(
        DisplayName = "AC18 an entity indexer is skipped without a diagnostic, the other columns map and the "
        + "model compiles")]
    public void An_indexer_is_not_a_column()
    {
        var (diagnostics, _, model) = Built(Indexer);

        Assert.Empty(diagnostics);
        Assert.Equal(["Id", "Title"], Columns(model, "A"));
    }

    [Fact(DisplayName = "AC18 a partial declaration is one entity with the members of both parts")]
    public void A_partial_entity_has_the_members_of_both_parts()
    {
        var (diagnostics, _, model) = Built(Partial);

        Assert.Empty(diagnostics);
        Assert.Equal(["Id", "Title"], Columns(model, "A"));
    }

    [Fact(DisplayName = "AC18 a constructor declared in the other part of a partial type is seen")]
    public void A_private_constructor_in_another_part_is_refused()
    {
        var (diagnostics, _, _) = Run(PartialWithConstructor);

        Assert.Equal(TypeId, Assert.Single(diagnostics).Id);
    }
}
