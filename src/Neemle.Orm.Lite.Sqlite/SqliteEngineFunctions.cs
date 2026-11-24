using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Neemle.Orm.Lite.Sqlite;

/// <summary>
/// Registers the library's own scalar functions through the engine API with SQLITE_INNOCUOUS, which
/// Microsoft.Data.Sqlite's CreateFunction cannot set. SQLite refuses a function that is not innocuous inside a
/// CHECK constraint, a view, a trigger or an index expression while PRAGMA trusted_schema is OFF, and the library
/// writes such objects (orm_decimal_fits in the CHECK of a decimal column, orm_json_* and orm_ilike in views).
/// </summary>
internal static class SqliteEngineFunctions
{
    internal const int Flags = SqliteFunctions.TextUtf8 | SqliteFunctions.Deterministic | SqliteFunctions.Innocuous;

    internal static void Scalar(sqlite3 handle, string name, int arity, Func<sqlite3_value[], object?> body) =>
        Check(
            name,
            raw.sqlite3_create_function(
                handle, name, arity, Flags, null, (context, _, args) => Run(context, args, body)));

    internal static void Check(string name, int result)
    {
        if (result != raw.SQLITE_OK)
            throw new SqliteException("the library function " + name + " could not be registered.", result);
    }

    private static void Run(sqlite3_context context, sqlite3_value[] args, Func<sqlite3_value[], object?> body)
    {
        try
        {
            SqliteEngineValues.Write(context, body(args));
        }
        catch (Exception fault)
        {
            raw.sqlite3_result_error(context, fault.Message);
        }
    }
}
