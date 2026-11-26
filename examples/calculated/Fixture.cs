using Neemle.OrmLite;

namespace CalculatedDemo;

internal sealed class Fixture(OrmContext db, SqlCounter counter, string? file) : IAsyncDisposable
{
    internal OrmContext Db { get; } = db;
    internal SqlCounter Counter { get; } = counter;
    internal static Guid Id(int suffix) => new("00000000-0000-0000-0000-" + suffix.ToString("D12"));
    internal static async Task<Fixture> OpenAsync(string provider)
    {
        if (provider is not ("sqlite" or "postgres")) throw new ArgumentException("Use sqlite or postgres");
        var file = provider == "sqlite" ? Path.Combine(Path.GetTempPath(), "calculated-" + Guid.NewGuid() + ".db") : null;
        var options = new OrmLiteOptions { Provider = provider, Path = file, Pooling = false };
        if (provider == "postgres") options.ConnectionString = Environment.GetEnvironmentVariable("CALCULATED_POSTGRES")
            ?? throw new InvalidOperationException("Set CALCULATED_POSTGRES to a disposable database");
        var counter = new SqlCounter();
        options.Diagnostics.Sink = counter;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        var db = await Neemle.OrmLite.OrmLite.OpenAsync(options);
        try
        {
            await using var command = db.DataSource.CreateCommand("DROP TABLE IF EXISTS demo_calculated_links; "
                + "DROP TABLE IF EXISTS demo_calculated_children; DROP TABLE IF EXISTS demo_calculated_tags; "
                + "DROP TABLE IF EXISTS demo_calculated_notes; DROP TABLE IF EXISTS schema_migrations;");
            await command.ExecuteNonQueryAsync();
            await db.MigrateAsync();
            return new Fixture(db, counter, file);
        }
        catch { await db.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        if (file is not null) foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(file + suffix);
    }
}
