using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal static class PolicyCleanup
{
    private static readonly ConditionalWeakTable<Exception, PolicySecondaryErrors> Secondary = new();
    internal static void Retain(Exception primary, Exception secondary)
    {
        if (ReferenceEquals(primary, secondary)) return;
        try { Secondary.GetValue(primary, _ => new PolicySecondaryErrors()).Add(secondary); }
        catch { } // Best-effort private retention must never replace the active failure.
    }
    internal static async ValueTask DisposeAsync(IAsyncDisposable resource, Exception? primary)
    {
        try { await resource.DisposeAsync(); }
        catch (Exception secondary) when (primary is not null) { Retain(primary, secondary); }
    }
    internal static async ValueTask DisposeConnectionAsync(DbConnection connection, Exception? primary)
    {
        var cleanup = new PolicyCleanupSteps(primary);
        await cleanup.RunAsync(connection.CloseAsync);
        await cleanup.RunAsync(() => connection.DisposeAsync().AsTask());
        if (primary is null) cleanup.ThrowIfFailed();
    }
}
