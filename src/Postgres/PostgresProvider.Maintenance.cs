using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Neemle.OrmLite.Sql;
using Npgsql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider : IMaintenanceProvider
{
    /// <summary>
    /// The maintenance budget when a request names none: 30 seconds, the SQLite default and Npgsql's default command
    /// timeout. A long VACUUM (FULL) or blocking REINDEX of a big table needs an explicit, longer <c>Timeout</c>.
    /// </summary>
    public static readonly TimeSpan DefaultMaintenanceTimeout = TimeSpan.FromSeconds(30);

    // lock_not_available: the lock_timeout the guard sets ran out.
    private const string LockNotAvailable = "55P03";

    private static readonly ConditionalWeakTable<DbDataSource, string> WriterKeys = new();

    public TimeSpan MaintenanceTimeout => DefaultMaintenanceTimeout;

    /// <summary>
    /// <c>postgres:host:port/database</c>, from the source's connection string. It never carries credentials or any
    /// other keyword. The host is lower-cased (DNS names are case-insensitive); the database name keeps its case, as
    /// PostgreSQL does.
    /// </summary>
    public string? WriterKeyOf(DbDataSource source) =>
        WriterKeys.GetValue(source, static each =>
        {
            var builder = new NpgsqlConnectionStringBuilder(each.ConnectionString);
            return "postgres:" + builder.Host?.ToLowerInvariant() + ":" + builder.Port.ToString(CultureInfo.InvariantCulture) + "/" + builder.Database;
        });

    /// <summary>PostgreSQL has no in-process writer gate: the lease is always entered and holds nothing.</summary>
    public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
        ValueTask.FromResult(new MaintenanceWriteLease(true, null));

    public bool IsContention(DbException failure) => failure is PostgresException { SqlState: LockNotAvailable };

    /// <summary><c>SET lock_timeout</c> to the remaining budget (at least 1 ms) and <c>RESET lock_timeout</c>.</summary>
    public MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining)
    {
        var milliseconds = Math.Max(1, (long)Math.Ceiling(remaining.TotalMilliseconds));
        var setup = new SqlWriter().Atom("SET").Atom(PostgresDdlDataLiterals.LockTimeoutSetting).Atom("=")
            .Str(milliseconds.ToString(CultureInfo.InvariantCulture) + "ms").ToString();
        var teardown = new SqlWriter().Atom("RESET").Atom(PostgresDdlDataLiterals.LockTimeoutSetting).ToString();
        return new MaintenanceSessionGuard(setup, teardown);
    }

    public string RenderVacuum(VacuumMode mode, EntityDescriptor? table)
    {
        var sql = new SqlWriter().Atom("VACUUM");
        switch (mode)
        {
            case VacuumMode.Plain:
                break;
            case VacuumMode.Full:
                sql.Open().Atom("FULL").Close();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a VACUUM mode.");
        }
        if (table is not null) sql.Atom(QuotedTable(table));
        return sql.ToString();
    }

    // The table exactly as every other statement of the provider names it.
    private static string QuotedTable(EntityDescriptor table) =>
        new PostgresSerializer().Fragment(new Nodes.TableRef(table.Table));
}
