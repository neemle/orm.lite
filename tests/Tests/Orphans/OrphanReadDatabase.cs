using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrphanReadDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "orphan-u1-" + Guid.NewGuid().ToString("N"));
    internal OrmContext Context { get; private set; } = null!;
    internal List<StatementEvent> Statements { get; } = [];
    internal static readonly Guid Root = new(1, 0, 0, new byte[8]);
    internal static readonly Guid Child = new(2, 0, 0, new byte[8]);

    internal static async Task<OrphanReadDatabase> Open()
    {
        var result = new OrphanReadDatabase();
        Directory.CreateDirectory(result._directory);
        SqliteProviderFactory.Register();
        var options = new OrmLiteOptions { Provider = "sqlite", Pooling = false,
            ConnectionString = "Data Source=" + Path.Combine(result._directory, "read.db") };
        options.Diagnostics.Sink = result;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        result.Context = await OrmLite.OpenAsync(options);
        await using var connection = await result.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE u1_rows(id TEXT PRIMARY KEY,parent_id TEXT,name TEXT NOT NULL);"
            + "CREATE TABLE u1_links(from_id TEXT,to_id TEXT,PRIMARY KEY(from_id,to_id));"
            + "INSERT INTO u1_rows VALUES(@root,NULL,'root'),(@child,@root,'child');"
            + "INSERT INTO u1_links VALUES(@root,@child);";
        command.AddParam("root", Root); command.AddParam("child", Child);
        await command.ExecuteNonQueryAsync();
        result.Statements.Clear();
        return result;
    }

    public void OnStatement(StatementEvent statement) => Statements.Add(statement);
    public void OnTransaction(TransactionEvent transaction) { }
    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        Directory.Delete(_directory, true);
    }
}
