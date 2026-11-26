using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Tests.Graph;

internal sealed class SdetTrackedConnection(string connectionString) : SqliteConnection(connectionString)
{
    internal bool WasDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return base.DisposeAsync();
    }
}
