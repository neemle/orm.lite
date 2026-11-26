using Neemle.OrmLite;
using Neemle.OrmLite.Sqlite;

namespace ForeignKeysAot;

internal static class Program
{
    private static async Task<int> Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "orml110-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            SqliteProviderFactory.Register();
            await using var db = await OrmLite.OpenAsync(new OrmLiteOptions
            {
                Provider = "sqlite",
                ConnectionString = "Data Source=" + Path.Combine(directory, "foreign-keys.db") + ";Pooling=False",
            });
            await NativeProbe.VerifyAsync(db);
            Console.WriteLine("foreign-key native runtime: " + Environment.Version);
            Console.WriteLine("foreign-key native ok");
            return 0;
        }
        finally { Directory.Delete(directory, true); }
    }
}
