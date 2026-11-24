using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class LostKeyGeneratorTests
{
    private const string KeyedMethods = "GetAsync UpdateAsync DeleteAsync";

    private const string OnlyKeyLost = """
        [Table("a")]
        public class A
        {
            public Guid Id { get; private set; }
            public string Title { get; set; } = "";
            public System.Collections.Generic.List<B> Bs { get; set; } = new();
        }

        [Table("bs")]
        public class B
        {
            [Key] public Guid Id { get; set; }
            public Guid AId { get; set; }
            public A? A { get; set; }
        }
        """;

    private const string SecondKeyLost = """
        [Table("lines")]
        public class Line
        {
            [Key] public Guid OrderId { get; set; }
            [Key] public Guid ProductId { get; private set; }
            public int Qty { get; set; }
        }
        """;

    private const string KeyWithPrivateGetter = """
        [Table("a")]
        public class A
        {
            [Key] public Guid Id { private get; set; }
            public string Title { get; set; } = "";
        }
        """;

    private const string SoftKeyLost = """
        [Table("docs")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class Doc
        {
            [Key] public Guid Id { get; private set; }
            public DateTime? DeletedAt { get; set; }
        }
        """;

    private const string LinkedToLostKey = """
        [Table("authors")]
        public class Author
        {
            [Key] public Guid Id { get; set; }
            [ManyToMany("author_tags", "author_id", "tag_id")]
            public System.Collections.Generic.List<Tag> Tags { get; set; } = new();
        }

        [Table("tags")]
        public class Tag
        {
            public Guid Id { get; private set; }
            public string Label { get; set; } = "";
        }
        """;

    private static string[] KeyedMembers(GeneratedModel model, string orm) =>
        Methods(model, orm).Where(name => KeyedMethods.Contains(name, StringComparison.Ordinal)).ToArray();

    [Fact(
        DisplayName = "AC17 a key with a private setter is refused and its entity has no keyed member and no "
        + "loader into or out of it")]
    public void A_lost_only_key_withholds_the_keyed_api_and_the_cascade()
    {
        var (diagnostics, _, model) = Built(OnlyKeyLost);

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Empty(KeyedMembers(model, "AOrm"));
        Assert.DoesNotContain("LoadBsAsync", Methods(model, "AOrm"));
        Assert.DoesNotContain("LoadAAsync", Methods(model, "BOrm"));
        Assert.Contains("GetAsync", Methods(model, "BOrm"));
    }

    [Fact(DisplayName = "AC17 a composite entity that loses one key gets the keyed api withheld and no ORMLITE022")]
    public void A_lost_second_key_withholds_the_keyed_api()
    {
        var (diagnostics, _, model) = Built(SecondKeyLost);

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Equal(["Column", "InsertAsync", "Map", "Query", "Values"], Methods(model, "LineOrm"));
    }

    [Fact(DisplayName = "AC17 a key with a private getter is a lost key too")]
    public void A_key_with_a_private_getter_is_lost()
    {
        var (diagnostics, _, model) = Built(KeyWithPrivateGetter);

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Empty(KeyedMembers(model, "AOrm"));
    }

    [Fact(
        DisplayName = "AC17 a soft-delete entity with a private-set key gets the property diagnostic and "
        + "ORMLITE008 with its text unchanged")]
    public void A_lost_soft_key_keeps_the_soft_delete_diagnostic()
    {
        var (diagnostics, _, model) = Built(SoftKeyLost);

        Assert.Equal(2, diagnostics.Length);
        Assert.Single(diagnostics, d => d.Id == PropertyId);
        var soft = Assert.Single(diagnostics, d => d.Id == "ORMLITE008");
        Assert.Equal(
            "[SoftDelete] on 'Doc' requires exactly one non-nullable System.Guid key property, but it has none",
            soft.GetMessage());
        Assert.Empty(KeyedMembers(model, "DocOrm"));
    }

    [Fact(DisplayName = "AC17 a many-to-many that targets an entity with a lost key is not generated")]
    public void A_many_to_many_into_a_lost_key_is_withheld()
    {
        var (diagnostics, _, model) = Built(LinkedToLostKey);

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        var methods = Methods(model, "AuthorOrm");
        Assert.DoesNotContain("LoadTagsAsync", methods);
        Assert.DoesNotContain("AddTagAsync", methods);
        Assert.DoesNotContain("RemoveTagAsync", methods);
    }
}
