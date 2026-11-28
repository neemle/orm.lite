namespace Neemle.OrmLite.Sqlite;

// The invariant pieces of the message a foreign-key finding carries:
// "<table> row <rowid> references a missing row in <parent> (foreign key <fkid>)".
internal static class SqliteIntegrityDataLiterals
{
    internal const string Row = " row ";

    internal const string References = " references a missing row in ";

    internal const string ForeignKey = " (foreign key ";

    internal const string End = ")";

    // The one row integrity_check and quick_check return for a sound database.
    internal const string Sound = "ok";
}
