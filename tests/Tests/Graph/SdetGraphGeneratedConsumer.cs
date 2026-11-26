using System.Reflection;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.Graph;

internal static class SdetGraphGeneratedConsumer
{
    private static readonly GeneratedModel Model = Compile();

    private const string Schema = """
        CREATE TABLE sdet_graph_parents (id TEXT PRIMARY KEY, name TEXT, secret TEXT);
        CREATE TABLE sdet_graph_children (id TEXT PRIMARY KEY, parent_id TEXT, editor_id TEXT, title TEXT);
        CREATE TABLE sdet_graph_int_parents (id INTEGER PRIMARY KEY);
        CREATE TABLE sdet_graph_int_children (id INTEGER PRIMARY KEY, int_parent_id INTEGER NULL);
        INSERT INTO sdet_graph_parents VALUES ('A', 'MiXeD', 'first'), ('B', NULL, 'second'), ('C', 'EMPTY', 'last');
        INSERT INTO sdet_graph_children VALUES ('one', 'A', 'B', 'ChIlD'), ('two', 'B', NULL, NULL),
            ('orphan', NULL, NULL, 'UNASSOCIATED');
        INSERT INTO sdet_graph_int_parents VALUES (1), (2);
        INSERT INTO sdet_graph_int_children VALUES (10, 1), (11, NULL);
        """;

    private static GeneratedModel Compile()
    {
        var (diagnostics, generated, output) = Run(SdetGraphGeneratedSource.Text);
        Assert.Empty(diagnostics);
        Assert.Contains("GraphCollectionBinding", generated);
        return GeneratedModel.Of(output);
    }

    internal static async Task SeedAsync(OrmContext db)
    {
        Model.RunModuleInitializer();
        Model.RunModuleInitializer();
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Schema;
        await command.ExecuteNonQueryAsync();
    }

    internal static Task<object[]> ReadAsync(OrmContext db, string method)
    {
        var operations = Model.Class("ParentOrm").Assembly.GetType("Fixture.Operations", true)!;
        try
        {
            return Assert.IsAssignableFrom<Task<object[]>>(operations.GetMethod(method)!.Invoke(null, [db]));
        }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    internal static Task UnsafeAsync(OrmContext db)
    {
        var operations = Model.Class("ParentOrm").Assembly.GetType("Fixture.Operations", true)!;
        try { return Assert.IsAssignableFrom<Task>(operations.GetMethod("Unsafe")!.Invoke(null, [db])); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    internal static async Task AssertReadAsync(OrmContext db)
    {
        var values = await ReadAsync(db, "Read");
        Assert.Equal(new string?[] { "mixed", null, "empty" }, Assert.IsType<string[]>(values[0]));
        Assert.Equal(new[] { 1, 1, 0 }, Assert.IsType<int[]>(values[1]));
        Assert.Equal("child", values[2]);
        Assert.True(Assert.IsType<bool>(values[3]));
        Assert.True(Assert.IsType<bool>(values[4]));
        Assert.Equal(new[] { 1, 0 }, Assert.IsType<int[]>(values[5]));
    }
}
