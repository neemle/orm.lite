using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.Graph;

internal sealed class GraphDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "orm-graph-" + Guid.NewGuid().ToString("N"));
    internal OrmContext Context { get; private set; } = null!;
    internal List<StatementEvent> Statements { get; } = [];

    internal static async Task<GraphDatabase> OpenAsync()
    {
        GraphDomain.Register();
        var result = new GraphDatabase();
        Directory.CreateDirectory(result._directory);
        SqliteProviderFactory.Register();
        var options = new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(result._directory, "graph.db") };
        options.Diagnostics.Sink = result;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        result.Context = await OrmLite.OpenAsync(options);
        await using var connection = await result.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = GraphDomain.Schema;
        await command.ExecuteNonQueryAsync();
        result.Statements.Clear();
        return result;
    }

    internal Query<GraphAuthor> Authors() => new(Context, GraphDomain.MapAuthor);
    public void OnStatement(StatementEvent statement) => Statements.Add(statement);
    public void OnTransaction(TransactionEvent transaction) { }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }
}
