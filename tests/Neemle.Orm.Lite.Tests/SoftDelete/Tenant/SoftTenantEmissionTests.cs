using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Tenant;

public sealed class SoftTenantEmissionTests
{
    private const string Plain = "[Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }";

    private const string PlainProps = "new string[] { \"Id\", \"id\" }, new string[] { \"Name\", \"name\" }, new string[] { \"DeletedAt\", \"deleted_at\" }";

    private const string ScopeProp = ", new string[] { \"ScopeId\", \"scope_id\" }";

    private static string Tail(string extraProps = "") =>
        ", EntityToken: \"doc\", PropertyColumns: new string[][] { " + PlainProps + extraProps + " }));";

    private static string Doc(string attribute, string members, string indexes = "") =>
        $"[Table(\"docs\")] {attribute} {indexes} public class Doc {{ {members} }}\n";

    private static string RegisterLine(string source)
    {
        var (diagnostics, generated, _) = Run(source);
        Assert.Empty(diagnostics);
        var start = generated.IndexOf("OrmMeta.Register<global::Fixture.Doc>(", StringComparison.Ordinal);
        Assert.True(start >= 0, "no OrmMeta.Register line for Doc");
        return generated.Substring(start, generated.IndexOf('\n', start) - start).TrimEnd('\r');
    }

    [Fact(DisplayName = "AC9-E: a [SoftDelete] with a ScopeId property and no Tenant registers TenantColumn scope_id")]
    public void The_default_tenant_is_emitted()
    {
        var line = RegisterLine(Doc("[SoftDelete]", Plain + " public Guid ScopeId { get; set; }"));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", TenantColumn: \"scope_id\", KeyColumn: \"id\"" + Tail(ScopeProp), line);
    }

    [Fact(DisplayName = "AC9-E: Tenant = \"Id\" registers TenantColumn id")]
    public void An_id_tenant_is_emitted()
    {
        var line = RegisterLine(Doc("[SoftDelete(Tenant = \"Id\")]", Plain));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", TenantColumn: \"id\", KeyColumn: \"id\"" + Tail(), line);
    }

    [Fact(DisplayName = "AC9-E: Tenant = Platform registers the NULL-tenant form and never a tenant column")]
    public void A_platform_tenant_is_emitted_as_platform()
    {
        var line = RegisterLine(Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", Plain + " public Guid ScopeId { get; set; }"));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", Platform: true, KeyColumn: \"id\"" + Tail(ScopeProp), line);
        Assert.DoesNotContain("TenantColumn", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC9-E: a tenant property with an explicit [Column] name is registered under its database column name")]
    public void The_tenant_column_is_the_database_column()
    {
        var line = RegisterLine(Doc("[SoftDelete(Tenant = \"OwnerId\")]", Plain + " [Column(\"owner_ref\")] public Guid OwnerId { get; set; }"));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", TenantColumn: \"owner_ref\", KeyColumn: \"id\"" + Tail(", new string[] { \"OwnerId\", \"owner_ref\" }"), line);
    }

    [Fact(DisplayName = "AC9-E: Unversioned registers exactly the listed columns, in order, as database column names")]
    public void Unversioned_columns_are_emitted()
    {
        var line = RegisterLine(Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform, Unversioned = new[] { \"Secret\", \"Token\" })]",
            Plain + " public string Secret { get; set; } = \"\"; [Column(\"api_token\")] public string Token { get; set; } = \"\";"));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", Platform: true, Unversioned: new string[] { \"secret\", \"api_token\" }, KeyColumn: \"id\"" + Tail(", new string[] { \"Secret\", \"secret\" }, new string[] { \"Token\", \"api_token\" }"), line);
    }

    [Fact(DisplayName = "AC9-E: the tenant and the unversioned set follow the LiveOnly index sets on the same registration")]
    public void The_tenant_follows_the_live_only_sets()
    {
        var line = RegisterLine(Doc("[SoftDelete]", Plain + " public Guid ScopeId { get; set; }",
            "[Index(\"Name\", Unique = true, LiveOnly = true)]"));

        Assert.EndsWith(", new global::Neemle.Orm.Lite.SoftDeleteMeta(\"DeletedAt\", new string[][] { new string[] { \"name\" } }, TenantColumn: \"scope_id\", KeyColumn: \"id\"" + Tail(ScopeProp), line);
    }

    [Fact(DisplayName = "AC10: an entity without [SoftDelete] keeps its plain Register line even when it has a ScopeId property")]
    public void A_plain_entity_keeps_its_register_line()
    {
        var line = RegisterLine(Doc("", Plain + " public Guid ScopeId { get; set; }"));

        Assert.EndsWith(", new string[] { \"id\", \"name\", \"deleted_at\", \"scope_id\",  });", line);
        Assert.DoesNotContain("SoftDeleteMeta", line, StringComparison.Ordinal);
    }
}
