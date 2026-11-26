using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class ActionCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var cascade = Guid.NewGuid(); var nullable = Guid.NewGuid(); var required = Guid.NewGuid();
        foreach (var id in new[] { cascade, nullable, required })
            await ActionParentOrm.InsertAsync(f.Db, new ActionParent { Id = id });
        var c = new CascadeDependent { Id = Guid.NewGuid(), ParentId = cascade };
        var n = new SetNullDependent { Id = Guid.NewGuid(), ParentId = nullable };
        var r = new RequiredDependent { Id = Guid.NewGuid(), ParentId = required };
        await CascadeDependentOrm.InsertAsync(f.Db, c);
        await SetNullDependentOrm.InsertAsync(f.Db, n);
        await RequiredDependentOrm.InsertAsync(f.Db, r);
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var loaded = (await RequiredDependentOrm.GetAsync(f.Db, r.Id))!;
        await RequiredDependentOrm.LoadParentAsync(f.Db, connection, loaded);
        Results.Require(loaded.Parent?.Id == required, "required FK resolves its principal");
        await ActionParentOrm.DeleteAsync(f.Db, cascade);
        Results.Require(await CascadeDependentOrm.GetAsync(f.Db, c.Id) is null, "explicit physical Cascade");
        await ActionParentOrm.DeleteAsync(f.Db, nullable);
        Results.Require((await SetNullDependentOrm.GetAsync(f.Db, n.Id))?.ParentId is null, "physical SetNull");
        await IntegrityCases.RejectAsync(() => ActionParentOrm.DeleteAsync(f.Db, required));
        await RequiredDependentOrm.DeleteAsync(f.Db, r.Id);
        Results.Require(await ActionParentOrm.GetAsync(f.Db, required) is not null,
            "dependent delete does not delete principal");
        Results.Case("actions", "required resolves|Cascade|SetNull|NoAction+Restrict refuse|dependent retains principal");
    }
}
