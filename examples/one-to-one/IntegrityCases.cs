using System.Data.Common;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class IntegrityCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await RejectAsync(() => ProfileOrm.InsertAsync(f.Db, new Profile { Id = Guid.NewGuid(), PrincipalId = 999 }));
        await RejectAsync(() => ProfileOrm.InsertAsync(f.Db, new Profile { Id = Guid.NewGuid(), PrincipalId = 1 }));
        var second = new Profile { Id = Guid.NewGuid(), PrincipalId = 3 };
        await ProfileOrm.InsertAsync(f.Db, second);
        second.PrincipalId = 1;
        await RejectAsync(() => ProfileOrm.UpdateAsync(f.Db, second));
        Results.Require((await ProfileOrm.GetAsync(f.Db, second.Id))?.PrincipalId == 3, "failed reassignment atomic");
        await RejectAsync(() => PersonOrm.DeleteAsync(f.Db, 1));
        var empty = new Profile { Id = Guid.NewGuid() };
        await ProfileOrm.InsertAsync(f.Db, empty);
        await ProfileOrm.InsertAsync(f.Db, new Profile { Id = Guid.NewGuid() });
        var loaded = (await ProfileOrm.GetAsync(f.Db, empty.Id))!;
        loaded.Person = new Person { Id = 888 };
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var before = f.Counter.Count;
        await ProfileOrm.LoadPersonAsync(f.Db, connection, loaded);
        Results.Require(loaded.Person is null && f.Counter.Count == before,
            "optional null clears without lookup");
        Results.Case("integrity", "orphan+duplicate refused|multiple NULLs|atomic reassignment|Restrict|null zero-query");
    }

    internal static async Task RejectAsync(Func<Task> action)
    {
        try { await action(); }
        catch (DbException) { Results.Require(true, "native integrity refusal"); return; }
        throw new InvalidOperationException("Expected native integrity refusal");
    }
}
