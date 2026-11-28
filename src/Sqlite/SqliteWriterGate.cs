using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteWriterGate
{
    private const int Busy = 5;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    internal static async ValueTask<IAsyncDisposable?> EnterAsync(string? key, TimeSpan timeout, CancellationToken ct)
    {
        if (key is null) return null;
        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(timeout, ct))
            throw new SqliteException(
                $"the single-writer gate of database '{key}' was not free within {timeout.TotalSeconds:0.###} seconds; "
                + "another unit of work or write holds the write lock, and a statement that ignores it would wait on the file lock instead.",
                Busy);
        return new Lease(gate);
    }

    // As EnterAsync, on the same gates, but a timeout is reported as null instead of thrown: maintenance treats a busy
    // gate as a result.
    internal static async ValueTask<IAsyncDisposable?> TryEnterAsync(string key, TimeSpan timeout, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        return await gate.WaitAsync(timeout, ct) ? new Lease(gate) : null;
    }

    private sealed class Lease(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
