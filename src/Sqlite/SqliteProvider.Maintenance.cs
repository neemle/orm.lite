using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider : IMaintenanceProvider
{
    private const int SqliteBusy = 5;

    private const int SqliteLocked = 6;

    private const string MainSchema = "main";

    /// <summary>The maintenance budget when a request names none: the provider's busy timeout.</summary>
    public TimeSpan MaintenanceTimeout => BusyTimeout;

    public string? WriterKeyOf(DbDataSource source) => SqliteDatabaseKey.Of(source);

    public async ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct)
    {
        if (SqliteDatabaseKey.Of(source) is not { } key) return new MaintenanceWriteLease(true, null);
        var release = await SqliteWriterGate.TryEnterAsync(key, timeout, ct);
        return new MaintenanceWriteLease(release is not null, release);
    }

    /// <summary>SQLite serialises in-process writers through the per-database writer gate.</summary>
    public bool HasWriterGate => true;

    /// <summary>SQLITE_BUSY and SQLITE_LOCKED, extended codes included, are contention.</summary>
    public bool IsContention(DbException failure) =>
        failure is SqliteException { SqliteErrorCode: var code } && (code & 0xFF) is SqliteBusy or SqliteLocked;

    /// <summary>
    /// One second: Microsoft.Data.Sqlite bounds its busy retry only by the command timeout, and a cancellation
    /// (sqlite3_interrupt) does not stop that loop, so a lock wait is taken one second at a time.
    /// </summary>
    public TimeSpan? ContentionRetrySlice => TimeSpan.FromSeconds(1);

    public string RenderWalCheckpoint(WalCheckpointMode mode) =>
        new SqlWriter().Atom("PRAGMA").Func("wal_checkpoint").Attach(CheckpointMode(mode)).Close().ToString();

    private static string CheckpointMode(WalCheckpointMode mode) => mode switch
    {
        WalCheckpointMode.Passive => "PASSIVE",
        WalCheckpointMode.Full => "FULL",
        WalCheckpointMode.Restart => "RESTART",
        WalCheckpointMode.Truncate => "TRUNCATE",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a WAL checkpoint mode."),
    };

    // The table exactly as every other statement of the provider names it.
    private string QuotedTable(EntityDescriptor table) => CreateSerializer().Fragment(new TableRef(table.Table));
}
