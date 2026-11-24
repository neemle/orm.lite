using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.SoftDelete.Tenant;

public sealed class SoftDeclarationRefusalTests
{
    private const string Key = "[Key] public Guid Id { get; set; }";
    private const string Soft = "public DateTime? DeletedAt { get; set; }";
    private const string Scope = "public Guid ScopeId { get; set; }";

    private static string Doc(string attribute, string members) =>
        $"[Table(\"docs\")] {attribute} public class Doc {{ {members} }}\n";

    private static string Refusal(string source, string id)
    {
        var (diagnostics, _, _) = Run(source);
        var refusal = Assert.Single(diagnostics, d => d.Id == id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("'Doc'", message, StringComparison.Ordinal);
        return message;
    }

    private static void Compiles(string source)
    {
        var (diagnostics, _, _) = Run(source);
        Assert.Empty(diagnostics);
    }

    [Fact(DisplayName = "AC9 ORMLITE007 refuses a [SoftDelete] entity with no tenant column and no Platform, naming the entity and the default tenant")]
    public void No_tenant_column_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete]", Key + Soft), "ORMLITE007");

        Assert.Contains("'ScopeId'", message, StringComparison.Ordinal);
        Assert.Contains("is not a mapped property of the entity", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE007 refuses a Tenant that names a property the entity does not have")]
    public void A_missing_tenant_property_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete(Tenant = \"OwnerId\")]", Key + Soft + Scope), "ORMLITE007");

        Assert.Contains("'OwnerId'", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE007 refuses a Tenant property that is not a System.Guid")]
    public void A_non_guid_tenant_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete(Tenant = \"OwnerName\")]", Key + Soft + " public string OwnerName { get; set; } = \"\";"), "ORMLITE007");

        Assert.Contains("'OwnerName'", message, StringComparison.Ordinal);
        Assert.Contains("not System.Guid", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE008 refuses a composite key, naming the entity and both key properties")]
    public void A_composite_key_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete]", Key + " [Key] public Guid OtherId { get; set; } " + Soft + Scope), "ORMLITE008");

        Assert.Contains("Id", message, StringComparison.Ordinal);
        Assert.Contains("OtherId", message, StringComparison.Ordinal);
        Assert.Contains("2", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE008 refuses a key that is not a System.Guid")]
    public void A_non_guid_key_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete]", "[Key] public int Id { get; set; } " + Soft + Scope), "ORMLITE008");

        Assert.Contains("'Id' is not a non-nullable System.Guid", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE008 refuses a nullable Guid key")]
    public void A_nullable_guid_key_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete]", "[Key] public Guid? Id { get; set; } " + Soft + Scope), "ORMLITE008");

        Assert.Contains("'Id' is not a non-nullable System.Guid", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE008 refuses an entity with no key property")]
    public void No_key_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete]", "public Guid Ident { get; set; } " + Soft + Scope), "ORMLITE008");

        Assert.Contains("it has none", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 ORMLITE009 refuses an Unversioned name that is not a mapped property, naming the entity and the name")]
    public void An_unknown_unversioned_name_is_refused()
    {
        var message = Refusal(Doc("[SoftDelete(Unversioned = new[] { \"Secret\", \"Missing\" })]",
            Key + Soft + Scope + " public string Secret { get; set; } = \"\";"), "ORMLITE009");

        Assert.Contains("'Missing'", message, StringComparison.Ordinal);
        Assert.DoesNotContain("'Secret'", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9 control: a Platform entity with no tenant column compiles without a diagnostic")]
    public void A_platform_entity_compiles() =>
        Compiles(Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", Key + Soft));

    [Fact(DisplayName = "AC9 control: an entity with the default ScopeId compiles without a diagnostic")]
    public void The_default_tenant_compiles() =>
        Compiles(Doc("[SoftDelete]", Key + Soft + Scope));

    [Fact(DisplayName = "AC9 control: Tenant = \"Id\" compiles without a diagnostic")]
    public void A_self_tenant_compiles() =>
        Compiles(Doc("[SoftDelete(Tenant = \"Id\")]", Key + Soft));

    [Fact(DisplayName = "AC9 control: a valid Unversioned list compiles without a diagnostic")]
    public void A_valid_unversioned_list_compiles() =>
        Compiles(Doc("[SoftDelete(Unversioned = new[] { \"Secret\" })]", Key + Soft + Scope + " public string Secret { get; set; } = \"\";"));
}
