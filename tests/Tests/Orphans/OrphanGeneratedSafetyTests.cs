using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGeneratedSafetyTests
{
    [Theory(DisplayName = "[UC-007][SEC-113-01] SDK C#13 custom and partial scalar accessors keep ordinary metadata without orphan admission")]
    [InlineData("PartialScalar")] [InlineData("CustomScalar")]
    public async Task Unsafe_scalar_accessors_are_not_registered_or_evaluated(string name)
    {
        var assembly = await Bootstrap();
        var type = Entity(assembly, name);
        Assert.NotNull(OrmMeta.ReadDescriptor(type));
        Assert.Contains("name", OrmMeta.Get(type).Columns);
        Assert.False(OrphanModel.CurrentForRead(type));
        Unread(assembly);
        await using var database = await OrphanReadDatabase.Open();
        await using var connection = await database.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        var table = OrmMeta.ReadDescriptor(type)!.Table;
        command.CommandText = "CREATE TABLE " + table + "(id TEXT PRIMARY KEY,name TEXT NOT NULL)";
        await command.ExecuteNonQueryAsync();
        var instance = Activator.CreateInstance(type)!;
        type.GetProperty("Id")!.SetValue(instance, new Guid(31, 0, 0, new byte[8]));
        try
        {
            var orm = assembly.GetType("Neemle.OrmLite.Generated." + name + "Orm", true)!;
            await (Task)orm.GetMethod("InsertAsync", [typeof(OrmContext), type])!.Invoke(null,
                [database.Context, instance])!;
            command.CommandText = "SELECT name FROM " + table;
            Assert.Equal(name == "PartialScalar" ? "partial" : "custom", await command.ExecuteScalarAsync());
            Assert.Equal(1, Entity(assembly, "Witness").GetField("Scalar")!.GetValue(null));
        }
        finally { Entity(assembly, "Witness").GetField("Scalar")!.SetValue(null, 0); }
    }

    [Theory(DisplayName = "[UC-007][AC-113-04] valid physical owner FKs do not authorize unsafe or custom collection navigation")]
    [InlineData("PartialNavigation", "PartialChild")]
    [InlineData("CustomNavigation", "CustomChild")]
    [InlineData("CustomCollection", "CollectionChild")]
    public async Task Unsafe_navigation_accessors_cannot_resolve_owned_collections(string parentName, string childName)
    {
        var assembly = await Bootstrap();
        var parent = Entity(assembly, parentName); var child = Entity(assembly, childName);
        var foreign = Assert.Single(OrmMeta.ReadDescriptor(child)!.ForeignKeys);
        Assert.Equal("parent_id", foreign.LocalColumn);
        Assert.Equal(OrmMeta.ReadDescriptor(parent)!.Table, foreign.Target.Table);
        var failure = Assert.Throws<OrphanException>(() => OrphanModel.Resolve(parent, child, "Children"));
        Assert.Equal(OrphanError.UnsupportedMapping, failure.Error);
        Assert.Equal(OrphanOutcome.NotStarted, failure.Outcome);
        Unread(assembly);
    }

    [Fact(DisplayName = "[UC-007][AC-113-04] implicit record scalar properties and a safe List retain generated registration")]
    public async Task Implicit_record_properties_have_a_complete_physical_payload()
    {
        var assembly = await Bootstrap();
        var parent = Entity(assembly, "RecordParent"); var child = Entity(assembly, "RecordChild");
        var binding = OrphanModel.Resolve(parent, child, "Children");
        var id = new Guid(23, 0, 0, new byte[8]); var owner = new Guid(24, 0, 0, new byte[8]);
        var instance = Activator.CreateInstance(child, id, owner, "record")!;
        Assert.Equal(id, binding.Child.Key(instance));
        Assert.Equal([id, owner, "record"], binding.Child.Values(instance));
        Unread(assembly);
    }

    private static async Task<Assembly> Bootstrap()
    {
        var assembly = await OrphanSafetyFixture.Compiled.Value;
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        return assembly;
    }

    private static Type Entity(Assembly assembly, string name) => assembly.GetType("OrphanSafetyConsumer." + name, true)!;
    private static void Unread(Assembly assembly)
    {
        var witness = Entity(assembly, "Witness");
        Assert.Equal(0, witness.GetField("Scalar")!.GetValue(null));
        Assert.Equal(0, witness.GetField("Navigation")!.GetValue(null));
    }
}
