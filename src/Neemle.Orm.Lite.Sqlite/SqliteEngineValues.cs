using SQLitePCL;

namespace Neemle.Orm.Lite.Sqlite;

/// <summary>
/// Reads the arguments of, and writes the result of, a function registered through the engine API. It converts
/// as Microsoft.Data.Sqlite does for the typed overloads of CreateFunction, so a function body sees the same
/// values whichever way it was registered: SQL NULL is null for a string and <see cref="DBNull"/> for an
/// untyped argument, INTEGER is long, FLOAT is double, TEXT is string and BLOB is byte[].
/// </summary>
internal static class SqliteEngineValues
{
    internal static string? TextOf(sqlite3_value value) =>
        raw.sqlite3_value_type(value) == raw.SQLITE_NULL ? null : raw.sqlite3_value_text(value).utf8_to_string();

    internal static long LongOf(sqlite3_value value) => raw.sqlite3_value_int64(value);

    internal static object ValueOf(sqlite3_value value)
    {
        var type = raw.sqlite3_value_type(value);
        if (type == raw.SQLITE_INTEGER) return raw.sqlite3_value_int64(value);
        if (type == raw.SQLITE_FLOAT) return raw.sqlite3_value_double(value);
        if (type == raw.SQLITE_TEXT) return raw.sqlite3_value_text(value).utf8_to_string();
        if (type == raw.SQLITE_BLOB) return raw.sqlite3_value_blob(value).ToArray();
        return DBNull.Value;
    }

    internal static void Write(sqlite3_context context, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                raw.sqlite3_result_null(context);
                break;
            case long whole:
                raw.sqlite3_result_int64(context, whole);
                break;
            case double real:
                raw.sqlite3_result_double(context, real);
                break;
            case string text:
                raw.sqlite3_result_text(context, text);
                break;
            default:
                throw new InvalidOperationException(
                    $"a library function returned {value.GetType().FullName}, which SQLite cannot hold.");
        }
    }
}
