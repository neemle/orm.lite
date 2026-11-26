using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace StreamingExample;

internal static class CancellationExport
{
    internal static async Task RunAsync(OrmContext db)
    {
        using var cancel = new CancellationTokenSource();
        try { await ReadPrefixAsync(NoteOrm.Query(db).OrderBy(n => n.Title).StreamAsync(cancel.Token), cancel); }
        catch (QueryStreamCanceledException error)
        {
            if (error.CleanupErrors.Count != 0) throw;
            Console.WriteLine("Canceled prefix: " + error.RowsYielded + "; cleanup complete");
        }
    }

    private static async Task ReadPrefixAsync(QueryStream<Note> stream, CancellationTokenSource cancel)
    {
        var cursor = stream.GetAsyncEnumerator();
        Exception? primary = null;
        try
        {
            if (await cursor.MoveNextAsync()) Console.WriteLine("Before cancellation: " + cursor.Current.Title);
            cancel.Cancel();
            await cursor.MoveNextAsync();
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            await cursor.DisposeAsync();
            var end = await cursor.Completion;
            if (!end.CleanupSucceeded && primary is null) throw new IOException("Cancellation cleanup failed.");
        }
    }
}
