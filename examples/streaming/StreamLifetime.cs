using Neemle.OrmLite;

namespace StreamingExample;

internal static class StreamLifetime
{
    internal static async Task<QueryStreamCompletion> PreviewAsync<T>(QueryStream<T> stream, Action<T> consume)
    {
        var cursor = stream.GetAsyncEnumerator();
        Exception? primary = null;
        QueryStreamCompletion completion;
        try
        {
            if (await cursor.MoveNextAsync()) consume(cursor.Current);
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            await cursor.DisposeAsync();
            completion = await cursor.Completion;
            if (!completion.CleanupSucceeded && primary is null)
                throw new IOException("Preview stream cleanup failed.");
        }
        return completion;
    }
}
