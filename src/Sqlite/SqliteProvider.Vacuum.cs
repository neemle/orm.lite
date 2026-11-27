using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    private const int SqliteBusy = 5;

    private const int SqliteLocked = 6;

    /// <summary>SQLite serialises in-process writers through the per-database writer gate.</summary>
    public bool HasWriterGate => true;

    /// <summary>SQLITE_BUSY and SQLITE_LOCKED, extended codes included, are contention.</summary>
    public bool IsContention(DbException failure) =>
        failure is SqliteException { SqliteErrorCode: var code } && (code & 0xFF) is SqliteBusy or SqliteLocked;

    /// <summary>Only a plain VACUUM of the whole database; FULL and a table are refused by capability first.</summary>
    public string RenderVacuum(VacuumMode mode, EntityDescriptor? table)
    {
        if (mode != VacuumMode.Plain || table is not null)
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "SQLite vacuums only the whole database, in Plain mode.");
        return new SqlWriter().Atom("VACUUM").ToString();
    }
}
