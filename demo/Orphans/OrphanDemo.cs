using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OrmLiteDemo;

internal static class OrphanDemo
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var provider = args.FirstOrDefault() ?? "sqlite";
        Console.WriteLine("orphans provider=" + provider + " runtime=" + Environment.Version);
        GraphDemoDatabase? scratch = null;
        var ok = false;
        try
        {
            var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET");
            // The CI legs name the SDK version (10.0.x, 11.0.x), a local run may name the bare major (10, 11): like
            // TestHostRuntimeTests, compare only the major before the first dot.
            if (expected is not null && Environment.Version.Major.ToString() != expected.Split('.')[0])
                throw new InvalidOperationException("Unexpected runtime");
            scratch = await GraphDemoDatabase.OpenAsync(provider);
            await RunCasesAsync(scratch.Context);
            ok = true;
        }
        catch (OrphanException failure) { Console.Error.WriteLine(failure.ToString()); }
        catch { Console.Error.WriteLine("orphans chapter failed"); }
        finally { if (scratch is not null) await scratch.DisposeAsync(); }
        ok &= scratch?.CleanupSucceeded == true;
        Console.WriteLine(ok ? "orphans chapter ok; scratch removed" : "orphans chapter FAILED");
        return ok ? 0 : 1;
    }
    private static async Task RunCasesAsync(OrmContext db)
    {
        var schema = OrmSchema.Of("demo_orphans", OrphanDemoParentOrm.Descriptor,
            OrphanDemoChildOrm.Descriptor, OrphanDemoSoftChildOrm.Descriptor);
        await db.MigrateAsync(schema.Contributions());
        await OrphanDemoDisabled.RunAsync(db);
        await OrphanDemoReplacement.RunAsync(db);
        await OrphanDemoPresent.RunAsync(db);
        await OrphanDemoSoft.RunAsync(db);
        await OrphanDemoRefusals.RunAsync(db);
        await OrphanDemoBounds.RunAsync(db);
        await OrphanDemoRollback.RunAsync(db);
        await OrphanDemoCancellation.RunAsync(db);
    }
}
