using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
namespace ComposeProbe;
internal sealed class Fixture(OrmContext db, Counter counter, string path) : IAsyncDisposable
{
    internal OrmContext Db { get; } = db;
    internal Counter Counter { get; } = counter;
    internal static readonly Guid ProfileId = new("00000000-0000-0000-0000-000000000001");
    internal static async Task<Fixture> OpenAsync()
    {
        Neemle.OrmLite.Sqlite.SqliteProviderFactory.Register();
        var path = Path.Combine(Path.GetTempPath(), "compose-"+Guid.NewGuid().ToString("N")+".db");
        var options = new OrmLiteOptions { Provider="sqlite", Path=path };
        var counter = new Counter();
        options.Diagnostics.Sink=counter;
        options.Diagnostics.StatementLevel=OrmLogLevel.Debug;
        var f = new Fixture(await Neemle.OrmLite.OrmLite.OpenAsync(options),counter,path);
        await f.SeedAsync();
        return f;
    }
    private async Task SeedAsync()
    {
        await using var c=await Db.DataSource.OpenConnectionAsync();
        await using var cmd=c.CreateCommand();
        cmd.CommandText="CREATE TABLE compose_groups(id INTEGER PRIMARY KEY,label TEXT);"
            +"CREATE TABLE compose_people(id INTEGER PRIMARY KEY,group_id INTEGER NOT NULL,note TEXT);"
            +"CREATE TABLE compose_profiles(id TEXT PRIMARY KEY,person_id INTEGER NULL,note TEXT);";
        await cmd.ExecuteNonQueryAsync();
        await GroupOrm.InsertAsync(Db,new Group { Id=1,Label="g" });
        await PersonOrm.InsertAsync(Db,new Person { Id=1,GroupId=1,Note="note" });
        await ProfileOrm.InsertAsync(Db,new Profile { Id=ProfileId,PersonId=1,Note="note" });
    }
    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        foreach(var suffix in new[]{"","-wal","-shm"}) File.Delete(path+suffix);
    }
}
