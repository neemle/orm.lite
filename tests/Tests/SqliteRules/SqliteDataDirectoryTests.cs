using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

[Collection("DataDirectory")]
public sealed class SqliteDataDirectoryTests
{
    private static string? Key(string source) => SqliteDatabaseKey.Of("Data Source=" + source);

    private static async Task WithDataDirectoryAsync(Func<string, Task> body)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ormlite-datadir-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previous = AppDomain.CurrentDomain.GetData("DataDirectory");
        AppDomain.CurrentDomain.SetData("DataDirectory", directory);
        try
        {
            await body(directory);
        }
        finally
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", previous);
            Directory.Delete(directory, true);
        }
    }

    private static async Task CreateTableAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE t (id INTEGER)";
        await command.ExecuteNonQueryAsync();
    }

    [Fact(DisplayName = "the engine opens |DataDirectory| and relative names under the data directory; the key agrees")]
    public Task The_engine_and_the_key_agree_on_the_data_directory() =>
        WithDataDirectoryAsync(async directory =>
        {
            await CreateTableAsync("Data Source=|DataDirectory|x.db;Pooling=False");
            await CreateTableAsync("Data Source=y.db;Pooling=False");

            Assert.True(File.Exists(Path.Combine(directory, "x.db")));
            Assert.True(File.Exists(Path.Combine(directory, "y.db")));
            Assert.Equal(Key(Path.Combine(directory, "x.db")), Key("|DataDirectory|x.db"));
            Assert.Equal(Key(Path.Combine(directory, "y.db")), Key("y.db"));
        });

    [Fact(DisplayName = "a data source made under a data directory pins the macro to the file the engine opens")]
    public Task A_data_source_pins_the_macro() =>
        WithDataDirectoryAsync(async directory =>
        {
            var name = "Data Source=|DataDirectory|z.db;Pooling=False";
            var source = new SqliteDataSource(name, SqliteProvider.MinimumVersion);
            await using (source)
            {
                await using var connection = source.CreateConnection();
                await connection.OpenAsync();
            }

            var pinned = new SqliteConnectionStringBuilder(source.ConnectionString).DataSource;
            Assert.True(File.Exists(Path.Combine(directory, "z.db")));
            Assert.Equal(Path.Combine(directory, "z.db"), pinned);
        });
}
