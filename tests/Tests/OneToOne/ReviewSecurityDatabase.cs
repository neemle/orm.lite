using Neemle.OrmLite.Tests.ForeignKeys;
using Neemle.OrmLite.Tests.Graph.Security;
using Neemle.OrmLite.Sqlite;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.OneToOne;

internal sealed class ReviewSecurityDatabase : IAsyncDisposable
{
    private readonly ForeignKeyDatabase _database;
    private readonly GatedDataSource _gate;
    private readonly DbDataSource? _sqlite;
    internal OrmContext Context { get; }
    internal SecuritySource Source { get; }
    internal SecuritySink Sink { get; } = new();
    internal bool ParametersDefaultOff { get; }
    internal ForeignKeyDatabase Raw => _database;

    private ReviewSecurityDatabase(ForeignKeyDatabase database)
    {
        _database = database;
        var options = new OrmLiteDiagnosticsOptions { Sink = Sink, StatementLevel = OrmLogLevel.Debug };
        ParametersDefaultOff = !options.LogParameters;
        var provider = database.Context.Provider;
        if (provider.Id == "sqlite")
            _sqlite = SqliteProviderFactory.Instance.CreateDataSource(new OrmLiteOptions
                { ConnectionString = database.Context.DataSource.ConnectionString }, DbLaneKind.Request);
        var raw = _sqlite ?? database.Context.DataSource;
        _gate = new GatedDataSource(provider, raw, raw, raw, OrmDiagnostics.Create(provider, options));
        Source = new SecuritySource(_gate);
        Context = new OrmContext(Source, provider);
    }

    internal static async Task<ReviewSecurityDatabase> OpenAsync(string provider) => new(provider == "sqlite"
        ? await ForeignKeyDatabase.SqliteAsync() : await ForeignKeyDatabase.PostgresAsync());

    public async ValueTask DisposeAsync()
    {
        try { await Context.DisposeAsync(); }
        finally { await DisposeSourcesAsync(); }
    }

    private async ValueTask DisposeSourcesAsync()
    {
        try { await Source.DisposeAsync(); }
        finally
        {
            try { await _gate.DisposeAsync(); }
            finally
            {
                try { if (_sqlite is not null) await _sqlite.DisposeAsync(); }
                finally { await _database.DisposeAsync(); }
            }
        }
    }
}
