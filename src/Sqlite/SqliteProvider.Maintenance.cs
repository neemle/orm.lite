using System.Data.Common;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider : IMaintenanceProvider
{
    /// <summary>The maintenance budget when a request names none: the provider's busy timeout.</summary>
    public TimeSpan MaintenanceTimeout => BusyTimeout;

    public string? WriterKeyOf(DbDataSource source) => SqliteDatabaseKey.Of(source);

    public async ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct)
    {
        if (SqliteDatabaseKey.Of(source) is not { } key) return new MaintenanceWriteLease(true, null);
        var release = await SqliteWriterGate.TryEnterAsync(key, timeout, ct);
        return new MaintenanceWriteLease(release is not null, release);
    }

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
}
