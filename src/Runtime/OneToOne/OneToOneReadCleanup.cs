using System.Data.Common;
using System.Runtime.ExceptionServices;

namespace Neemle.OrmLite;

internal static class OneToOneReadCleanup
{
    internal static async ValueTask DisposeAsync(DbDataReader? reader, DbCommand? command, Exception? primary)
    {
        Exception? failure = null;
        if (reader is not null) failure = await AttemptAsync(reader.DisposeAsync);
        if (command is not null) failure = await AttemptAsync(command.DisposeAsync) ?? failure;
        if (primary is null && failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static async ValueTask<Exception?> AttemptAsync(Func<ValueTask> dispose)
    {
        try { await dispose(); return null; }
        catch (Exception error) { return error; }
    }
}
