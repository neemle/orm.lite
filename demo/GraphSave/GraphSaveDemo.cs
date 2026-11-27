using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphSaveDemo
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var provider = args.FirstOrDefault() ?? "sqlite";
        Console.WriteLine("graph-save provider=" + provider + " runtime=" + Environment.Version);
        GraphDemoDatabase? scratch = null;
        var ok = false;
        try
        {
            var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET");
            if (expected is not null && Environment.Version.Major.ToString() != expected)
                throw new InvalidOperationException("Unexpected runtime");
            scratch = await GraphDemoDatabase.OpenAsync(provider);
            await RunCasesAsync(scratch.Context);
            ok = true;
        }
        catch { Console.Error.WriteLine("graph-save chapter failed"); }
        finally { if (scratch is not null) await scratch.DisposeAsync(); }
        ok &= scratch?.CleanupSucceeded == true;
        Console.WriteLine(ok ? "graph-save chapter ok; scratch removed" : "graph-save chapter FAILED");
        return ok ? 0 : 1;
    }

    private static async Task RunCasesAsync(OrmContext db)
    {
        var schema = OrmSchema.Of("demo_graph_save", GraphSaveTeamOrm.Descriptor, GraphSaveProjectOrm.Descriptor,
            GraphSaveTaskOrm.Descriptor, GraphSaveTagOrm.Descriptor);
        await db.MigrateAsync(schema.Contributions());
        var fixture = new GraphSaveFixture();
        await GraphSaveDefaultCase.RunAsync(db);
        await GraphSaveInsertCase.RunAsync(db, fixture);
        await GraphSaveRepeatCase.RunAsync(db, fixture);
        await GraphSaveRollbackCase.RunAsync(db, fixture);
        await GraphSaveRefusalCase.RunAsync(db, fixture);
    }
}
