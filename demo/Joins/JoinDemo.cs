using Neemle.OrmLite;

namespace OrmLiteDemo;

internal static class JoinDemo
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var provider = args.FirstOrDefault() ?? "sqlite";
        GraphDemoDatabase? scratch = null;
        var ok = false;
        try
        {
            var expected = Environment.GetEnvironmentVariable("ORMLITE_EXPECT_DOTNET");
            JoinDemoChecks.Require(expected is null || Environment.Version.Major.ToString() == expected);
            scratch = await GraphDemoDatabase.OpenAsync(provider);
            Console.WriteLine("joins provider=" + scratch.Context.Provider.Id + " runtime=" + Environment.Version);
            await JoinDemoCases.RunAsync(scratch.Context);
            ok = true;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error is JoinReadException or JoinReadCanceledException
                ? error.ToString() : "joins failed: " + error.GetType().Name);
        }
        finally { if (scratch is not null) await scratch.DisposeAsync(); }
        ok &= scratch?.CleanupSucceeded == true;
        Console.WriteLine(ok ? "joins chapter ok; scratch removed" : "joins chapter FAILED");
        return ok ? 0 : 1;
    }
}
