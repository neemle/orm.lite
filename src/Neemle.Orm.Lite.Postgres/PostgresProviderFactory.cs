using System.Data.Common;
using Npgsql;

namespace Neemle.Orm.Lite.Postgres;

public sealed class PostgresProviderFactory : IProviderFactory
{
    public static readonly PostgresProviderFactory Instance = new();

    public string Id => "postgres";

    public static void Register() => ProviderRegistry.Register(Instance);

    public ISqlProvider CreateProvider(OrmLiteOptions options)
    {
        Refuse(options.Path, nameof(options.Path));
        Refuse(options.BusyTimeoutSeconds, nameof(options.BusyTimeoutSeconds));
        return PostgresProvider.Instance;
    }

    public DbDataSource CreateDataSource(OrmLiteOptions options, DbLaneKind lane) =>
        NpgsqlDataSource.Create(ConnectionStringOf(options));

    public async Task InitializeAsync(OrmLiteOptions options, DbDataSource source, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await PostgresServerVersion.RequireAsync(connection, ct);
    }

    internal static string ConnectionStringOf(OrmLiteOptions options)
    {
        Refuse(options.Path, nameof(options.Path));
        Refuse(options.BusyTimeoutSeconds, nameof(options.BusyTimeoutSeconds));
        var builder = string.IsNullOrWhiteSpace(options.ConnectionString)
            ? new NpgsqlConnectionStringBuilder()
            : new NpgsqlConnectionStringBuilder(options.ConnectionString);
        if (options.Host is not null) builder.Host = options.Host;
        if (options.Port is { } port) builder.Port = port;
        if (options.Database is not null) builder.Database = options.Database;
        if (options.Username is not null) builder.Username = options.Username;
        if (options.Password is not null) builder.Password = options.Password;
        if (options.Pooling is { } pooling) builder.Pooling = pooling;
        if (string.IsNullOrWhiteSpace(builder.Host))
            throw new InvalidOperationException("provider 'postgres' needs OrmLite:Host (or OrmLite:ConnectionString).");
        return builder.ConnectionString;
    }

    private static void Refuse(object? value, string option)
    {
        if (value is not null)
            throw new InvalidOperationException($"OrmLite:{option} does not apply to provider 'postgres'; remove it.");
    }
}
