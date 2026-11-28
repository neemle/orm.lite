using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    /// <summary>
    /// <c>PRAGMA integrity_check(N+1)</c> / <c>quick_check(N+1)</c> of the database, <c>PRAGMA main.integrity_check(&lt;table&gt;)</c>
    /// / <c>quick_check(&lt;table&gt;)</c> of one table (SQLite 3.33 or later), or <c>PRAGMA foreign_key_check</c>, of
    /// the database or <c>(&lt;table&gt;)</c>.
    /// </summary>
    public string RenderIntegrityCheck(IntegrityCheckMode mode, EntityDescriptor? table, int maxFindings)
    {
        var pragma = mode switch
        {
            IntegrityCheckMode.Full => "integrity_check",
            IntegrityCheckMode.Quick => "quick_check",
            IntegrityCheckMode.ForeignKeys => "foreign_key_check",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not an integrity check mode."),
        };
        var sql = new SqlWriter().Atom("PRAGMA").Atom(MainSchema).Dot();
        if (table is not null) return sql.Func(pragma).Attach(QuotedTable(table)).Close().ToString();
        if (mode == IntegrityCheckMode.ForeignKeys) return sql.Attach(pragma).ToString();
        return sql.Func(pragma).Int((long)maxFindings + 1).Close().ToString();
    }

    /// <summary>
    /// 100 for a table-scoped <c>Full</c> or <c>Quick</c> check: that form takes the table name where the limit would go,
    /// so the engine stops at its own error maximum (SQLITE_INTEGRITY_CHECK_ERROR_MAX); null for every other form.
    /// </summary>
    public int? IntegrityEngineCap(IntegrityCheckMode mode, EntityDescriptor? table) =>
        table is not null && mode is IntegrityCheckMode.Full or IntegrityCheckMode.Quick ? TableCheckMessageCap : null;

    private const int TableCheckMessageCap = 100;

    public IntegrityFinding? ReadIntegrityFinding(IntegrityCheckMode mode, DbDataReader row)
    {
        if (mode != IntegrityCheckMode.ForeignKeys)
        {
            var text = row.GetString(0);
            return text == SqliteIntegrityDataLiterals.Sound ? null : new IntegrityFinding(mode, text);
        }
        var table = row.GetString(0);
        long? rowId = row.IsDBNull(1) ? null : row.GetInt64(1);
        var parent = row.GetString(2);
        var foreignKey = (int)row.GetInt64(3);
        return new IntegrityFinding(mode, ForeignKeyMessage(table, rowId, parent, foreignKey), table, rowId, parent, foreignKey);
    }

    private static string ForeignKeyMessage(string table, long? rowId, string parent, int foreignKey) =>
        table
        + (rowId is { } id ? SqliteIntegrityDataLiterals.Row + id.ToString(CultureInfo.InvariantCulture) : "")
        + SqliteIntegrityDataLiterals.References + parent
        + SqliteIntegrityDataLiterals.ForeignKey + foreignKey.ToString(CultureInfo.InvariantCulture)
        + SqliteIntegrityDataLiterals.End;
}
