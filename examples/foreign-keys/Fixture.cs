using System.Data.Common;
using Neemle.OrmLite;

namespace ForeignKeysDemo;

internal sealed class Fixture(OrmContext db, SqlCounter counter, string? path) : IAsyncDisposable
{
    internal OrmContext Db { get; } = db;
    internal SqlCounter Counter { get; } = counter;
    internal string? SqlitePath { get; } = path;
    internal static Guid Id(int number) => new("00000000-0000-0000-0000-" + number.ToString("D12"));
    internal static async Task<Fixture> OpenAsync(string provider)
    {
        if (provider is not ("sqlite" or "postgres")) throw new ArgumentException("Use sqlite or postgres");
        var path = provider == "sqlite" ? Path.Combine(Path.GetTempPath(), "fk-demo-" + Guid.NewGuid() + ".db") : null;
        var options = new OrmLiteOptions { Provider = provider, Path = path, Pooling = false };
        if (provider == "postgres") options.ConnectionString = Environment.GetEnvironmentVariable("FOREIGN_KEYS_POSTGRES")
            ?? throw new InvalidOperationException("Set FOREIGN_KEYS_POSTGRES to a disposable database");
        var counter = new SqlCounter();
        options.Diagnostics.Sink = counter;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        var db = await Neemle.OrmLite.OrmLite.OpenAsync(options);
        var fixture = new Fixture(db, counter, path);
        try { await fixture.ResetAsync(); await db.MigrateAsync(); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }
    private async Task ResetAsync()
    {
        foreach (var table in new[] { "demo_fk_links", "demo_fk_setnull_children", "demo_fk_cascade_children",
            "demo_fk_restrict_children", "demo_fk_no_action_children", "demo_fk_loader_children",
            "demo_fk_soft_children", "demo_fk_soft_parents", "demo_fk_tags", "demo_fk_parents",
            "demo_fk_upgrade_children", "demo_fk_upgrade_parents", "schema_migrations" })
            await ExecuteAsync("DROP TABLE IF EXISTS \"" + table + "\"");
    }
    internal DbCommand Command(string sql, params object?[] values)
    {
        var command = Db.DataSource.CreateCommand(sql);
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "p" + i;
            parameter.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
    internal async Task<int> ExecuteAsync(string sql, params object?[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteNonQueryAsync();
    }
    internal async Task<object?> ScalarAsync(string sql, params object?[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteScalarAsync();
    }
    internal async Task<IReadOnlyList<CatalogForeignKey>> KeysAsync(string table)
    {
        await using var connection = await Db.DataSource.OpenConnectionAsync();
        return await Db.Provider.CreateCatalog(connection).ForeignKeysAsync(table);
    }
    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        if (SqlitePath is not null)
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(SqlitePath + suffix);
    }
}
