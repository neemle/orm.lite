namespace StreamingExample;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var storage = new ScratchStore();
        try
        {
            var server = args.Contains("--postgres")
                ? Environment.GetEnvironmentVariable("STREAMING_POSTGRES")
                    ?? throw new InvalidOperationException("Set STREAMING_POSTGRES for a scratch server.") : null;
            await storage.OpenAsync(server);
            await NoteExport.RunAsync(storage.Context);
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Streaming example failed; original errors are not printed.");
            return 1;
        }
        finally { await storage.DisposeAsync(); }
        if (!storage.CleanupSucceeded) { Console.Error.WriteLine("Scratch cleanup failed."); return 1; }
        Console.WriteLine("Scratch resources removed.");
        Console.WriteLine("streaming example ok; runtime=" + Environment.Version);
        return 0;
    }
}
