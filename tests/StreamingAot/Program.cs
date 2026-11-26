using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.StreamingAot;

SqliteProviderFactory.Register();
PostgresProviderFactory.Register();
var path = Path.Combine(Path.GetTempPath(), "streaming-aot-" + Guid.NewGuid().ToString("N") + ".db");
try
{
    await StreamNativeChecks.RunAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
    var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
        ?? throw new InvalidOperationException("isolated PostgreSQL fixture required");
    await StreamNativeChecks.RunAsync(new OrmLiteOptions { Provider = "postgres", ConnectionString = server });
    Console.WriteLine("STREAMING_NATIVE_PASS runtime=" + Environment.Version + " providers=sqlite,postgres");
    return 0;
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    if (File.Exists(path)) File.Delete(path);
}
