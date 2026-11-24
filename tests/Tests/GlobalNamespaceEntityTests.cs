using Neemle.OrmLite.Sqlite;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class GlobalNamespaceEntityTests
{
    private const string Usings = "using System; using System.Collections.Generic; using Neemle.OrmLite;\n";

    private const string GlobalEntities = @"
public enum Shade { Light, Dark }

[Table(""gardens"")]
public class Garden
{
    [Key] public Guid Id { get; set; }
    public string Name { get; set; } = """";
    public Shade Shade { get; set; }
    public List<Bed> Beds { get; set; } = new();
}

[Table(""beds"")]
public class Bed
{
    [Key] public Guid Id { get; set; }
    public Guid GardenId { get; set; }
    public int Size { get; set; }
    public Garden? Garden { get; set; }
}
";

    private const string NamespacedEntity = @"
namespace Inner
{
    [Table(""sheds"")]
    public class Shed
    {
        [Key] public Guid Id { get; set; }
        public string Label { get; set; } = """";
    }
}
";

    [Fact(DisplayName = "an entity in the global namespace generates without a diagnostic and the generated code compiles against it")]
    public void A_global_namespace_entity_generates_and_compiles()
    {
        var (diagnostics, generated, output) = RunWhole(Usings + GlobalEntities);

        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Equal("gardens", model.Descriptor("Garden").Table);
        Assert.Equal("beds", model.Descriptor("Bed").Table);
        Assert.Contains("global::Garden", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("global::.", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("<global namespace>", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "global and namespaced entities side by side each keep their own qualified name in the descriptor")]
    public void A_global_and_a_namespaced_entity_coexist()
    {
        var (diagnostics, generated, output) = RunWhole(Usings + GlobalEntities + NamespacedEntity);

        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Equal("sheds", model.Descriptor("Shed").Table);
        Assert.Equal("gardens", model.Descriptor("Garden").Table);
        Assert.Contains("global::Inner.Shed", generated, StringComparison.Ordinal);
        Assert.Contains("global::Garden", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the generated repository of a global-namespace entity writes and reads a row on SQLite")]
    public async Task A_global_namespace_entity_round_trips()
    {
        var (_, _, output) = RunWhole(Usings + GlobalEntities);
        var model = GeneratedModel.Of(output);
        var folder = Directory.CreateTempSubdirectory("ormlite-global-").FullName;
        try
        {
            SqliteProviderFactory.Register();
            await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(folder, "g.db") });
            await db.MigrateAsync(model.Contributions);
            var orm = model.Class("GardenOrm");
            var garden = Activator.CreateInstance(orm.Assembly.GetType("Garden", throwOnError: true)!)!;
            dynamic g = garden;
            var id = Guid.NewGuid();
            g.Id = id;
            g.Name = "front";
            var insert = orm.GetMethods().Single(m => m.Name == "InsertAsync" && m.GetParameters().Length == 2);
            await (Task)insert.Invoke(null, [db, garden])!;
            var get = orm.GetMethods().Single(m => m.Name == "GetAsync" && m.GetParameters().Length == 2);
            var task = (Task)get.Invoke(null, [db, id])!;
            await task;
            dynamic? found = task.GetType().GetProperty("Result")!.GetValue(task);
            Assert.Equal("front", (string)found!.Name);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(folder, true);
        }
    }
}
