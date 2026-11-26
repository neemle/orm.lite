using System.Data.Common;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecurityDatabase : IAsyncDisposable
{
    private readonly DbDataSource _raw;
    private readonly NpgsqlDataSource? _admin;
    private readonly string _storage;
    internal SecuritySink Sink { get; } = new();
    internal SecuritySource Source { get; }
    internal OrmContext Context { get; }

    private SecurityDatabase(DbDataSource raw, ISqlProvider provider, string storage, NpgsqlDataSource? admin,
        bool logParameters)
    {
        _raw = raw;
        _admin = admin;
        _storage = storage;
        Source = new SecuritySource(raw);
        var options = new OrmLiteDiagnosticsOptions { Sink = Sink, StatementLevel = OrmLogLevel.Debug,
            LogParameters = logParameters, ParameterMaxLength = 256 };
        var diagnostics = OrmDiagnostics.Create(provider, options);
        var gated = new GatedDataSource(provider, Source, Source, Source, diagnostics);
        Context = new OrmContext(gated, provider);
        SecurityModel.Register();
        SecurityModel.MappedRoots = 0;
    }

    internal static async Task<SecurityDatabase> SqliteAsync(bool logParameters)
    {
        var directory = Path.Combine(Path.GetTempPath(), SecurityModel.Credential + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var options = new OrmLiteOptions { Path = Path.Combine(directory, "fixture.db"), Pooling = false };
        var factory = SqliteProviderFactory.Instance;
        var result = new SecurityDatabase(factory.CreateDataSource(options, DbLaneKind.Request),
            factory.CreateProvider(options), directory, null, logParameters);
        await SecuritySeed.CreateAsync(result._raw);
        return result;
    }

    internal static async Task<SecurityDatabase> PostgresAsync(bool logParameters)
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")!;
        var admin = NpgsqlDataSource.Create(server);
        var name = "sec108_" + Guid.NewGuid().ToString("N");
        await using (var command = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
            await command.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(server)
            { Database = name, Password = SecurityModel.Credential, Pooling = false };
        var result = new SecurityDatabase(NpgsqlDataSource.Create(connection.ConnectionString),
            PostgresProvider.Instance, name, admin, logParameters);
        await SecuritySeed.CreateAsync(result._raw);
        return result;
    }

    internal async Task AssertWitnessAsync()
    {
        await using var command = _raw.CreateCommand("SELECT marker FROM sec108_witness");
        Xunit.Assert.Equal("alive", await command.ExecuteScalarAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Context.DataSource.DisposeAsync();
        await Source.DisposeAsync();
        await _raw.DisposeAsync();
        if (_admin is null) { Directory.Delete(_storage, true); return; }
        try
        {
            await using var command = _admin.CreateCommand("DROP DATABASE \"" + _storage + "\" WITH (FORCE)");
            await command.ExecuteNonQueryAsync();
        }
        finally { await _admin.DisposeAsync(); }
    }
}
