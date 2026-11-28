using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed partial class MaintenanceExecution
{
    // C6: every path unwinds the resources its effects registered, last first: the reader, the commands, the guard's
    // teardown, the connection, the gate lease. A cleanup that fails is ignored: cleanup never reads or changes the
    // outcome.
    private static async Task UnwindAsync(ResourceFrame frame)
    {
        while (frame.TryPop(out var cleanup))
        {
            try
            {
                await cleanup();
            }
            catch (Exception)
            {
            }
        }
    }

    // The guard's teardown gets one second (the guard grace) and no token: the connection is disposed next, and the
    // provider's reset on close is the second line.
    private static async ValueTask ResetAsync(DbConnection connection, string teardown)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = teardown;
        command.CommandTimeout = GuardGraceSeconds;
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    // The cleanup stack of one step.
    private sealed class ResourceFrame
    {
        private readonly Stack<Func<ValueTask>> _cleanups = new();

        internal void Push(IAsyncDisposable? resource)
        {
            if (resource is not null) _cleanups.Push(resource.DisposeAsync);
        }

        internal void PushReset(DbConnection connection, string teardown) =>
            _cleanups.Push(() => ResetAsync(connection, teardown));

        internal bool TryPop(out Func<ValueTask> cleanup) => _cleanups.TryPop(out cleanup!);
    }
}
