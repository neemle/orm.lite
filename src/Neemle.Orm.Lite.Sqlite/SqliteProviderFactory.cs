using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Sqlite;

public sealed class SqliteProviderFactory : IProviderFactory
{
    public static readonly SqliteProviderFactory Instance = new();

    public string Id => "sqlite";

    public static void Register() => ProviderRegistry.Register(Instance);

    public ISqlProvider CreateProvider(OrmLiteOptions options) => new SqliteProvider(BusyTimeoutOf(options));

    public DbDataSource CreateDataSource(OrmLiteOptions options, DbLaneKind lane) =>
        new SqliteDataSource(ConnectionStringOf(options), SqliteProvider.MinimumVersion);

    public async Task InitializeAsync(OrmLiteOptions options, DbDataSource source, CancellationToken ct)
    {
        if (SqliteDatabaseKey.Of(source) is null) return;
        await using var connection = source.CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = new SqlWriter().Atom("PRAGMA").Atom("journal_mode").Atom("=").Atom("WAL").ToString();
        await command.ExecuteScalarAsync(ct);
    }

    internal static TimeSpan BusyTimeoutOf(OrmLiteOptions options)
    {
        if (options.BusyTimeoutSeconds is not { } seconds) return SqliteProvider.DefaultBusyTimeout;
        if (seconds < 1)
            throw new InvalidOperationException(
                $"OrmLite:BusyTimeoutSeconds is {seconds}, and a timeout below one second is refused: 0 makes SQLite wait forever on a busy database.");
        return TimeSpan.FromSeconds(seconds);
    }

    internal static string ConnectionStringOf(OrmLiteOptions options)
    {
        Refuse(options.Host, nameof(options.Host));
        Refuse(options.Port, nameof(options.Port));
        Refuse(options.Database, nameof(options.Database));
        Refuse(options.Username, nameof(options.Username));
        Refuse(options.Password, nameof(options.Password));
        var builder = string.IsNullOrWhiteSpace(options.ConnectionString)
            ? new SqliteConnectionStringBuilder { DataSource = RequirePath(options), Mode = SqliteOpenMode.ReadWriteCreate }
            : new SqliteConnectionStringBuilder(options.ConnectionString);
        var given = Keywords(builder.ConnectionString);
        if (!given.Contains("foreignkeys")) builder.ForeignKeys = true;
        if (!given.Contains("recursivetriggers")) builder.RecursiveTriggers = true;
        if (!given.Contains("defaulttimeout") && !given.Contains("commandtimeout"))
            builder.DefaultTimeout = (int)BusyTimeoutOf(options).TotalSeconds;
        if (options.Pooling is { } pooling) builder.Pooling = pooling;
        return builder.ConnectionString;
    }

    private static HashSet<string> Keywords(string connectionString) =>
        new DbConnectionStringBuilder { ConnectionString = connectionString }.Keys.Cast<string>()
            .Select(key => string.Concat(key.Where(c => c != ' ')).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private static string RequirePath(OrmLiteOptions options) =>
        string.IsNullOrWhiteSpace(options.Path)
            ? throw new InvalidOperationException("provider 'sqlite' needs OrmLite:Path (the database file) or OrmLite:ConnectionString.")
            : options.Path;

    private static void Refuse(object? value, string option)
    {
        if (value is not null)
            throw new InvalidOperationException($"OrmLite:{option} does not apply to provider 'sqlite'; remove it.");
    }
}
