using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Tenant;

public sealed class SoftTenantEmissionTests
{
    private const string Plain = "[Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }";

    private static readonly string[] PlainProps = ["Id>id", "Name>name", "DeletedAt>deleted_at"];

    private static string Doc(string attribute, string members, string indexes = "") =>
        $"[Table(\"docs\")] {attribute} {indexes} public class Doc {{ {members} }}\n";

    private static SoftDeleteMeta Meta(string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output).Soft("Doc")!;
    }

    private static IEnumerable<string> Properties(SoftDeleteMeta meta) => meta.PropertyColumns!.Select(p => p[0] + ">" + p[1]);

    [Fact(DisplayName = "AC9-E: a [SoftDelete] with a ScopeId property and no Tenant registers TenantColumn scope_id")]
    public void The_default_tenant_is_emitted()
    {
        var meta = Meta(Doc("[SoftDelete]", Plain + " public Guid ScopeId { get; set; }"));

        Assert.Equal("scope_id", meta.TenantColumn);
        Assert.False(meta.Platform);
        Assert.Equal("id", meta.KeyColumn);
        Assert.Equal([.. PlainProps, "ScopeId>scope_id"], Properties(meta));
    }

    [Fact(DisplayName = "AC9-E: Tenant = \"Id\" registers TenantColumn id")]
    public void An_id_tenant_is_emitted()
    {
        var meta = Meta(Doc("[SoftDelete(Tenant = \"Id\")]", Plain));

        Assert.Equal("id", meta.TenantColumn);
        Assert.Equal("id", meta.KeyColumn);
        Assert.Equal(PlainProps, Properties(meta));
    }

    [Fact(DisplayName = "AC9-E: Tenant = Platform registers the NULL-tenant form and never a tenant column")]
    public void A_platform_tenant_is_emitted_as_platform()
    {
        var meta = Meta(Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", Plain + " public Guid ScopeId { get; set; }"));

        Assert.True(meta.Platform);
        Assert.Null(meta.TenantColumn);
        Assert.Equal([.. PlainProps, "ScopeId>scope_id"], Properties(meta));
    }

    [Fact(DisplayName = "AC9-E: a tenant property with an explicit [Column] name is registered under its database column name")]
    public void The_tenant_column_is_the_database_column()
    {
        var meta = Meta(Doc("[SoftDelete(Tenant = \"OwnerId\")]", Plain + " [Column(\"owner_ref\")] public Guid OwnerId { get; set; }"));

        Assert.Equal("owner_ref", meta.TenantColumn);
        Assert.Equal([.. PlainProps, "OwnerId>owner_ref"], Properties(meta));
    }

    [Fact(DisplayName = "AC9-E: Unversioned registers exactly the listed columns, in order, as database column names")]
    public void Unversioned_columns_are_emitted()
    {
        var meta = Meta(Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Unversioned = new[] { \"Secret\", \"Token\" })]",
            Plain + " public string Secret { get; set; } = \"\"; [Column(\"api_token\")] public string Token { get; set; } = \"\";"));

        Assert.True(meta.Platform);
        Assert.Equal(new[] { "secret", "api_token" }, meta.Unversioned);
        Assert.Equal([.. PlainProps, "Secret>secret", "Token>api_token"], Properties(meta));
    }

    [Fact(DisplayName = "AC9-E: the tenant and the unversioned set are registered together with the LiveOnly index sets")]
    public void The_tenant_follows_the_live_only_sets()
    {
        var meta = Meta(Doc("[SoftDelete]", Plain + " public Guid ScopeId { get; set; }",
            "[Index(\"Name\", Unique = true, LiveOnly = true)]"));

        Assert.Equal(new[] { new[] { "name" } }, meta.LiveOnlyIndexes);
        Assert.Equal("scope_id", meta.TenantColumn);
        Assert.Equal("id", meta.KeyColumn);
    }

    [Fact(DisplayName = "AC10: an entity without [SoftDelete] registers its columns and no soft metadata even when it has a ScopeId property")]
    public void A_plain_entity_registers_no_soft_metadata()
    {
        var (diagnostics, _, output) = Run(Doc("", Plain + " public Guid ScopeId { get; set; }"));
        Assert.Empty(diagnostics);

        var model = GeneratedModel.Of(output);

        Assert.Equal(new[] { "id", "name", "deleted_at", "scope_id" }, model.Descriptor("Doc").ColumnNames);
        Assert.Null(model.Soft("Doc"));
    }
}
