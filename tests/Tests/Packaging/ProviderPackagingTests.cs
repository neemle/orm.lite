using System.Xml.Linq;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Packaging;

public sealed class ProviderPackagingTests
{
    public static TheoryData<string, Type> Providers => new()
    {
        { "postgres", typeof(PostgresProviderFactory) },
        { "sqlite", typeof(SqliteProviderFactory) },
    };

    private static string Package(Type factory) => factory.Assembly.GetName().Name!;

    // The project folder is the assembly name without the library root: Neemle.OrmLite.Postgres is src/Postgres.
    private static string Folder(Type factory) => Package(factory)["Neemle.OrmLite.".Length..];

    private static string Assets(Type factory) => Path.Combine(RuntimeSources.LibraryRoot(), "src", Folder(factory), "assets");

    private static XElement Declaration(Type factory) =>
        XDocument.Load(Path.Combine(Assets(factory), Package(factory) + ".props")).Descendants()
            .Single(e => e.Name.LocalName == "OrmLiteProviderPackage");

    [Theory(DisplayName = "the props file a provider package ships declares that package, its provider id and its factory type exactly")]
    [MemberData(nameof(Providers))]
    public void The_props_file_declares_the_provider(string id, Type factory)
    {
        var declaration = Declaration(factory);

        Assert.Equal(Package(factory), declaration.Attribute("Include")!.Value);
        Assert.Equal("Neemle.OrmLite." + char.ToUpperInvariant(id[0]) + id[1..], declaration.Attribute("Include")!.Value);
        Assert.Equal(id, declaration.Attribute("Provider")!.Value);
        Assert.Equal("global::" + factory.FullName, declaration.Attribute("Factory")!.Value);
        Assert.Equal(id, ((IProviderFactory)factory.GetField("Instance")!.GetValue(null)!).Id);
    }

    [Theory(DisplayName = "the factory the build emits a call for has a public static Register that makes the provider resolvable")]
    [MemberData(nameof(Providers))]
    public void The_emitted_call_registers_the_provider(string id, Type factory)
    {
        var register = factory.GetMethod("Register", Type.EmptyTypes);

        Assert.NotNull(register);
        Assert.True(register!.IsStatic && register.IsPublic);
        register.Invoke(null, null);
        register.Invoke(null, null);
        Assert.Equal(id, ProviderRegistry.Require(id).Id);
        Assert.Contains(id, ProviderRegistry.Installed);
    }

    [Theory(DisplayName = "a provider package passes its capability manifest to the compiler under the file name the generator reads")]
    [MemberData(nameof(Providers))]
    public void The_manifest_travels_under_the_name_the_generator_reads(string id, Type factory)
    {
        var additional = XDocument.Load(Path.Combine(Assets(factory), Package(factory) + ".props")).Descendants()
            .Single(e => e.Name.LocalName == "AdditionalFiles").Attribute("Include")!.Value;
        var name = "ormlite.capabilities." + id + ".txt";

        Assert.Equal("$(MSBuildThisFileDirectory)" + name, additional);
        Assert.True(File.Exists(Path.Combine(Assets(factory), name)));
    }

    [Theory(DisplayName = "a provider project packs under its assembly name, ships its props and manifest in buildTransitive, and depends on the core package")]
    [MemberData(nameof(Providers))]
    public void The_project_packs_what_the_build_needs(string id, Type factory)
    {
        var project = XDocument.Load(Path.Combine(RuntimeSources.LibraryRoot(), "src", Folder(factory), Folder(factory) + ".csproj"));
        var naming = XDocument.Load(Path.Combine(RuntimeSources.LibraryRoot(), "src", "Directory.Build.props"));
        string Prop(XDocument file, string name) => file.Descendants().Single(e => e.Name.LocalName == name).Value;
        var packed = project.Descendants().Where(e => e.Name.LocalName == "None" && e.Attribute("Pack")?.Value == "true")
            .Select(e => e.Attribute("Include")!.Value + ">" + e.Attribute("PackagePath")!.Value).ToList();

        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName is "PackageId" or "AssemblyName");
        Assert.Equal("$(AssemblyName)", Prop(naming, "PackageId"));
        Assert.Equal("$(OrmLiteRoot).$(MSBuildProjectName)", Prop(naming, "AssemblyName"));
        Assert.Contains("assets/" + Package(factory) + ".props>buildTransitive/", packed);
        Assert.Contains("assets/ormlite.capabilities." + id + ".txt>buildTransitive/", packed);
        var core = project.Descendants().Single(e => e.Name.LocalName == "ProjectReference" && e.Attribute("Include")!.Value.EndsWith("Core/Core.csproj", StringComparison.Ordinal));
        Assert.Equal("none", core.Attribute("PrivateAssets")!.Value);
        Assert.Null(core.Attribute("ReferenceOutputAssembly"));
    }

    [Fact(DisplayName = "the core build targets emit one reflection-free module initializer that calls Register on each referenced provider factory, unless opted out")]
    public void The_targets_emit_the_registration()
    {
        var targets = File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), "src", "Core", "assets", "Neemle.OrmLite.targets"));

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", targets, StringComparison.Ordinal);
        Assert.Contains("%(Factory).Register()", targets, StringComparison.Ordinal);
        Assert.Contains("'$(OrmLiteRegisterProviders)' != 'false'", targets, StringComparison.Ordinal);
        Assert.Contains("%(Provider)", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("Activator", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("GetType(", targets, StringComparison.Ordinal);
    }
}
