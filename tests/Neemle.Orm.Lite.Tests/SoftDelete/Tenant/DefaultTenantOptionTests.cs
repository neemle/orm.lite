using System.Collections.Immutable;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SoftDelete.Tenant;

public sealed class DefaultTenantOptionTests
{
    private const string Header = "using System; using Neemle.Orm.Lite; namespace Fixture;\n";

    private const string Key = "[Key] public Guid Id { get; set; }";

    private const string Soft = "public DateTime? DeletedAt { get; set; }";

    private const string ScopeMember = "public Guid ScopeId { get; set; }";

    private const string TenantMember = "public Guid TenantId { get; set; }";

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated, ImmutableArray<Diagnostic> CompileErrors) Run(
        string source, AnalyzerConfigOptionsProvider options)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var compilation = CSharpCompilation.Create(
            "DefaultTenantFixture",
            [CSharpSyntaxTree.ParseText(Header + source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator()], null, null, options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();
        var generated = string.Join("\n", run.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        return (run.Diagnostics, generated, output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray());
    }

    private static string Doc(string attribute, string tenantMember) =>
        $"[Table(\"docs\")] {attribute} public class Doc {{ {Key} {Soft} {tenantMember} }}\n";

    [Fact(DisplayName = "OrmLiteDefaultTenant unset or empty leaves the generator output equal to the library default TenantId")]
    public void Unset_equals_the_library_default()
    {
        var source = Doc("[SoftDelete]", TenantMember);

        var unset = Run(source, GeneratorOptions.Unset());
        var empty = Run(source, GeneratorOptions.Tenant("  "));
        var named = Run(source, GeneratorOptions.Tenant("TenantId"));

        Assert.Empty(unset.Diagnostics);
        Assert.Empty(unset.CompileErrors);
        Assert.Contains("\"tenant_id\"", unset.Generated, StringComparison.Ordinal);
        Assert.Equal(unset.Generated, empty.Generated);
        Assert.Equal(unset.Generated, named.Generated);
    }

    [Fact(DisplayName = "OrmLiteDefaultTenant set to ScopeId changes the generator output only where the tenant name appears")]
    public void A_set_property_changes_only_the_tenant_name()
    {
        var baseline = Run(Doc("[SoftDelete]", TenantMember), GeneratorOptions.Unset());
        var pinned = Run(Doc("[SoftDelete]", ScopeMember), GeneratorOptions.Pinned());

        Assert.Empty(pinned.Diagnostics);
        Assert.Empty(pinned.CompileErrors);
        Assert.NotEqual(baseline.Generated, pinned.Generated);
        Assert.Equal(baseline.Generated, pinned.Generated.Replace("ScopeId", "TenantId").Replace("scope_id", "tenant_id"));
    }

    [Fact(DisplayName = "a set OrmLiteDefaultTenant does not change an entity that declares its own Tenant or Platform")]
    public void An_explicit_tenant_ignores_the_option()
    {
        var explicitTenant = Doc("[SoftDelete(Tenant = \"OwnerId\")]", "public Guid OwnerId { get; set; }");
        var platform = Doc("[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", "");

        Assert.Equal(Run(explicitTenant, GeneratorOptions.Unset()).Generated, Run(explicitTenant, GeneratorOptions.Pinned()).Generated);
        Assert.Equal(Run(platform, GeneratorOptions.Unset()).Generated, Run(platform, GeneratorOptions.Pinned()).Generated);
        Assert.Empty(Run(explicitTenant, GeneratorOptions.Pinned()).Diagnostics);
    }

    [Fact(DisplayName = "ORMLITE007 text under the unset default names TenantId and under a pinned tenant it names the pinned property")]
    public void The_missing_tenant_text_interpolates_the_default()
    {
        const string Pinned =
            "[SoftDelete] on 'Doc' has no usable tenant: Tenant 'ScopeId' is not a mapped property of the entity. "
            + "Declare a System.Guid tenant property (ScopeId by default, or name it with Tenant = ...), "
            + "or declare Tenant = SoftDeleteAttribute.Platform for an entity with no tenant.";
        var source = Doc("[SoftDelete]", "");

        var unset = Assert.Single(Run(source, GeneratorOptions.Unset()).Diagnostics, d => d.Id == "ORMLITE007");
        var pinned = Assert.Single(Run(source, GeneratorOptions.Pinned()).Diagnostics, d => d.Id == "ORMLITE007");

        Assert.Equal(Pinned.Replace("ScopeId", "TenantId"), unset.GetMessage());
        Assert.Equal(Pinned, pinned.GetMessage());
    }

    [Theory(DisplayName = "ORMLITE014 refuses a OrmLiteDefaultTenant that is not a C# identifier instead of falling back to the default")]
    [InlineData("not valid!")]
    [InlineData("1Tenant")]
    [InlineData("class")]
    [InlineData("Ten-ant")]
    [InlineData("<platform>")]
    public void A_value_that_is_not_an_identifier_is_refused(string value)
    {
        var result = Run(Doc("[SoftDelete]", TenantMember), GeneratorOptions.Tenant(value));

        var refusal = Assert.Single(result.Diagnostics, d => d.Id == "ORMLITE014");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'" + value + "'", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORMLITE014 is reported even when the project has no entities")]
    public void The_refusal_does_not_depend_on_an_entity()
    {
        Assert.Single(Run("", GeneratorOptions.Tenant("not valid!")).Diagnostics, d => d.Id == "ORMLITE014");
        Assert.Empty(Run("", GeneratorOptions.Tenant("TenantId")).Diagnostics);
    }
}
