using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class GraphDemo
{
    internal static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine("graph chapter runtime=" + Environment.Version);
        GraphDemoDatabase? scratch = null;
        var ok = false;
        try
        {
            var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET");
            if (expected is not null && Environment.Version.Major.ToString() != expected)
                throw new InvalidOperationException("Unexpected runtime");
            scratch = await GraphDemoDatabase.OpenAsync(args.FirstOrDefault() ?? "sqlite");
            ok = await ChapterAsync(scratch.Context);
        }
        catch { Console.Error.WriteLine("graph chapter failed"); }
        finally { if (scratch is not null) await scratch.DisposeAsync(); }
        ok = ok && scratch?.CleanupSucceeded == true;
        Console.WriteLine(ok ? "graph chapter ok; scratch removed" : "graph chapter FAILED");
        return ok ? 0 : 1;
    }

    private static async Task<bool> ChapterAsync(OrmContext db)
    {
        var schema = OrmSchema.Of("demo_graph_chapter", GraphAuthorOrm.Descriptor,
            GraphBookOrm.Descriptor, GraphTagOrm.Descriptor);
        await db.MigrateAsync(schema.Contributions());
        return await GraphChapter.RunAsync(db);
    }
}
