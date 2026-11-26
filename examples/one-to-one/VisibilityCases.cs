using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class VisibilityCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var parent = new SoftPrincipal { Id = Guid.NewGuid(), Name = "visible" };
        var child = new SoftDependent { Id = Guid.NewGuid(), ParentId = parent.Id, Name = "child" };
        await SoftPrincipalOrm.InsertAsync(f.Db, parent);
        await SoftDependentOrm.InsertAsync(f.Db, child);
        var source = (await SoftPrincipalOrm.GetAsync(f.Db, parent.Id))!;
        var owner = (await SoftDependentOrm.GetAsync(f.Db, child.Id))!;
        var old = new SoftDependent(); source.Child = old;
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var before = f.Counter.Count;
        await GuardCases.RefuseAsync(() => SoftPrincipalOrm.LoadChildAsync(f.Db, connection, source),
            OneToOneError.UnsupportedVisibility);
        Results.Require(f.Counter.Count == before && ReferenceEquals(old, source.Child),
            "filtered inverse refuses without SQL or publication");
        await SoftPrincipalOrm.DeleteAsync(f.Db, parent.Id);
        before = f.Counter.Count;
        await SoftDependentOrm.LoadParentAsync(f.Db, connection, owner);
        Results.Require(owner.Parent is null && f.Counter.Count == before + 1,
            "owner keeps live filter and performs no unrestricted probe");
        Results.Require(await SoftDependentOrm.GetAsync(f.Db, child.Id) is not null,
            "soft parent deletion does not physically cascade");
        Results.Case("visibility", "filtered inverse refused:zero SQL|owner live absence:one SQL|no soft cascade");
    }
}
