using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace Neemle.Orm.Lite.Sqlite;

internal static class SqliteDatabaseKey
{
    private static readonly ConditionalWeakTable<DbDataSource, string> Fixed = new();

    internal static string? Of(DbDataSource source)
    {
        var key = Fixed.GetValue(source, static each => Of(each.ConnectionString) ?? string.Empty);
        return key.Length == 0 ? null : key;
    }

    internal static string? Of(string connectionString) => Of(connectionString, DataDirectory());

    internal static string? Of(string connectionString, string? dataDirectory)
    {
        var (file, memory) = SqliteDataSourceName.Opened(connectionString, dataDirectory);
        if (memory is not null) return "memory:" + memory;
        return file is null ? null : SqliteFileIdentity.Canonical(file);
    }

    internal static string Pin(string connectionString) => Pin(connectionString, DataDirectory());

    internal static string Pin(string connectionString, string? dataDirectory)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (SqliteDataSourceName.Pinned(builder, dataDirectory) is not { } file) return connectionString;
            builder.DataSource = file;
            return builder.ConnectionString;
        }
        catch (Exception refusal) when (refusal is ArgumentException or IOException or NotSupportedException)
        {
            return connectionString;
        }
    }

    private static string? DataDirectory() => AppDomain.CurrentDomain.GetData("DataDirectory") as string;
}
