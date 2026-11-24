using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Generated;

namespace PackConsumer;

public static class ProviderProbe
{
    public static async Task<string> Report()
    {
        var lines = new[]
        {
            "installed=" + string.Join(",", ProviderRegistry.Installed),
            "sqlite=" + await Guarded(Sqlite),
            "postgres=" + await Guarded(Postgres),
            "unset=" + await Guarded(Unset),
        };
        return string.Join('\n', lines);
    }

    private static async Task<string> Guarded(Func<Task<string>> probe)
    {
        try
        {
            return await probe();
        }
        catch (Exception error)
        {
            return error.GetType().Name + ": " + error.Message.Replace('\n', ' ');
        }
    }

    private static async Task<string> Sqlite()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "probe-" + Guid.NewGuid().ToString("N") + ".db");
        await using var db = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = path });
        await db.MigrateAsync();
        await ProbeDocOrm.InsertAsync(db, new ProbeDoc { Id = Guid.NewGuid(), Name = "first" });
        var rows = await ProbeDocOrm.Query(db).Where(d => d.Name == "first").ToListAsync();
        await ProbeGlobalDocOrm.InsertAsync(db, new ProbeGlobalDoc { Id = Guid.NewGuid(), Name = "global" });
        var global = await ProbeGlobalDocOrm.Query(db).Where(d => d.Name == "global").ToListAsync();
        return "ok rows=" + rows.Count + " provider=" + db.Provider.Id + " global=" + global.Count;
    }

    private static async Task<string> Postgres()
    {
        await using var db = await OrmLite.OpenAsync(new OrmLiteOptions
        {
            Provider = "postgres", Host = "127.0.0.1", Port = 1, Database = "probe", Username = "probe", Pooling = false,
        });
        return "ok provider=" + db.Provider.Id;
    }

    private static async Task<string> Unset()
    {
        await using var db = await OrmLite.OpenAsync(new OrmLiteOptions());
        return "ok";
    }
}
