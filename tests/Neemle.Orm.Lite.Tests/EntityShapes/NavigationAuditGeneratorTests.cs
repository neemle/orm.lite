using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.MemberShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class NavigationAuditGeneratorTests
{
    private const string NotLoadable = "ORMLITE026";

    private const string StringOwner = """
        [Table("owners")]
        public class Owner
        {
            [Key] public string Id { get; set; } = "";
        }

        """;

    private const string LongOwner = """
        [Table("owners")]
        public class Owner
        {
            [Key] public long Id { get; set; }
        }

        """;

    private const string Keyless = """
        [Table("tags")]
        public class Tag
        {
            public string Label { get; set; } = "";
        }

        """;

    private const string KeyedTag = """
        [Table("tags")]
        public class Tag
        {
            [Key] public Guid Id { get; set; }
        }

        """;

    private static string PetWith(string members) =>
        "[Table(\"pets\")] public class Pet { [Key] public Guid Id { get; set; } " + members + " }\n";

    [Fact(DisplayName = "ORML-17 a nullable string foreign key compiles and the loader does not read .Value")]
    public void A_nullable_string_foreign_key_has_no_value_member()
    {
        var (diagnostics, generated, model) = Built(
            StringOwner + PetWith("public string? OwnerId { get; set; } public Owner? Owner { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Contains("LoadOwnerAsync", Methods(model, "PetOrm"));
        Assert.DoesNotContain("OwnerId.Value", Flat(generated), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-17 a nullable value type foreign key still reads .Value after the null test")]
    public void A_nullable_guid_foreign_key_keeps_its_value_member()
    {
        var (diagnostics, generated, _) = Built(
            Owner + PetWith("public Guid? OwnerId { get; set; } public Owner? Owner { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Contains("OwnerId.Value", Flat(generated), StringComparison.Ordinal);
    }

    [Theory(
        DisplayName = "ORML-17 a reference navigation whose foreign key is not a property gets ORMLITE026 naming "
        + "both and no loader")]
    [InlineData("public Owner? Owner { get; set; }", "OwnerId")]
    [InlineData("[Reference(\"OwnerRef\")] public Owner? Owner { get; set; }", "OwnerRef")]
    public void A_missing_foreign_key_is_reported(string members, string key)
    {
        var (diagnostics, _, model) = Built(Owner + PetWith(members));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NotLoadable, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Pet.Owner'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'" + key + "'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Pet", SpanText(refusal));
        AssertPlainMessage(refusal.GetMessage());
        Assert.DoesNotContain("LoadOwnerAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "ORML-17 a foreign key of another type than the target key gets ORMLITE026")]
    public void A_foreign_key_of_the_wrong_type_is_reported()
    {
        var (diagnostics, _, model) = Built(
            Owner + PetWith("public int OwnerId { get; set; } public Owner? Owner { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NotLoadable, refusal.Id);
        Assert.Contains("is int but the key 'Id' of 'Owner' is System.Guid", refusal.GetMessage());
        Assert.DoesNotContain("LoadOwnerAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "ORML-17 an int foreign key to a long key widens and keeps the loader")]
    public void A_widening_foreign_key_keeps_the_loader()
    {
        var (diagnostics, _, model) = Built(
            LongOwner + PetWith("public int OwnerId { get; set; } public Owner? Owner { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Contains("LoadOwnerAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "ORML-17 a navigation to an entity without a key gets ORMLITE026 and no loader")]
    public void A_keyless_target_is_reported()
    {
        var (diagnostics, _, model) = Built(
            Keyless + PetWith("public Guid TagId { get; set; } public Tag? Tag { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NotLoadable, refusal.Id);
        Assert.Contains("'Tag' has no key property", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("LoadTagAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "ORML-17 a navigation to a view gets ORMLITE026 and no loader")]
    public void A_view_target_is_reported()
    {
        var (diagnostics, _, model) = Built(
            "[View(\"vs\")] public class V { public Guid Id { get; set; } }\n"
            + PetWith("public Guid VId { get; set; } [Reference(\"VId\")] public V? V { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NotLoadable, refusal.Id);
        Assert.Contains("'V' is a view", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("LoadVAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "ORML-17 an inverse key of another type than the parent key gets ORMLITE026")]
    public void An_inverse_key_of_the_wrong_type_is_reported()
    {
        var (diagnostics, _, output) = Run(ParentOf("public int PId { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NotLoadable, refusal.Id);
        Assert.Contains("'P.Kids'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("is int but the key 'Id' of 'P' is System.Guid", refusal.GetMessage());
        var methods = Methods(GeneratedModel.Of(output), "POrm");
        Assert.DoesNotContain("LoadKidsAsync", methods);
        Assert.Contains("LoadOthersAsync", methods);
    }

    [Fact(DisplayName = "ORML-17 a collection and a many-to-many list of an entity without a key get ORMLITE026")]
    public void A_keyless_parent_loses_both_loaders()
    {
        var (diagnostics, _, output) = Run(
            Kid + KeyedTag + "[Table(\"parents\")] public class Parent { public string Name { get; set; } = \"\"; "
            + Kids + " { get; set; } = new(); " + TagLink + Tags + " { get; set; } = new(); }\n");

        Assert.Equal([NotLoadable, NotLoadable], diagnostics.Select(d => d.Id).ToArray());
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'Parent.Kids'", StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'Parent.Tags'", StringComparison.Ordinal));
        var methods = Methods(GeneratedModel.Of(output), "ParentOrm");
        Assert.DoesNotContain("LoadKidsAsync", methods);
        Assert.DoesNotContain("LoadTagsAsync", methods);
    }

    [Fact(DisplayName = "ORML-17 a foreign key another diagnostic already reports gets no second ORMLITE026")]
    public void A_foreign_key_with_a_private_setter_is_reported_once()
    {
        var (diagnostics, _, _) = Run(
            Owner + PetWith("public Guid OwnerId { get; private set; } public Owner? Owner { get; set; }"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
    }
}
