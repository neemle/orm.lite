using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    /// <summary>Only a plain VACUUM of the whole database; FULL and a table are refused by capability first.</summary>
    public string RenderVacuum(VacuumMode mode, EntityDescriptor? table)
    {
        if (mode != VacuumMode.Plain || table is not null)
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "SQLite vacuums only the whole database, in Plain mode.");
        return new SqlWriter().Atom("VACUUM").ToString();
    }
}
