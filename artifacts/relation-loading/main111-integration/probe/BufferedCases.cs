using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
namespace ComposeProbe;
internal static class BufferedCases
{
    internal static async Task Run(Fixture f)
    {
        var profile=(await ProfileOrm.Query(f.Db).ToListAsync()).Single();
        await using var c=await f.Db.DataSource.OpenConnectionAsync();
        await ProfileOrm.LoadPersonAsync(f.Db,c,profile);
        Check.That(profile.Person?.Id==1 && profile.Person.Group is null,"111 explicit lookup does not add eager work");
        var person=(await PersonOrm.Query(f.Db).FirstOrDefaultAsync())!;
        Check.That(person.Group?.Id==1,"109 eager path remains active");
        await PersonOrm.LoadProfileAsync(f.Db,c,person);
        Check.That(person.Profile?.Id==Fixture.ProfileId,"policy root keeps exact full provenance");
        var option=(await ProfileOrm.GetAsync(f.Db,Fixture.ProfileId,new RelationReadOptions()))!;
        await ProfileOrm.LoadPersonAsync(f.Db,c,option);
        Check.That(option.Person?.Id==1,"unconfigured policy options full map provenance");
        Check.That(typeof(ProfileOrm).GetMethods().Count(m=>m.Name=="LoadPersonAsync")==2,
            "only two guarded pair overloads");
        Check.That(!typeof(ProfileOrm.Paths).GetFields().Any(p=>p.Name=="Person"),"pair excluded from graph paths");
    }
    internal static async Task Scoped(Fixture f)
    {
        await using var unit=await UnitOfWork.BeginAsync(f.Db);
        var limits=new GraphLimits(new(4,16,32),new(10,100,20));
        await using var scope=LoadingScope.Bind(f.Db,unit,new(limits,4,TimeSpan.FromMinutes(1)));
        var person=(await PersonOrm.GetAsync(f.Db,unit.Connection,1,new(EagerBehavior.Suppress,scope)))!;
        Check.That(person.Group is null,"invocation suppression retained");
        await PersonOrm.LoadProfileAsync(f.Db,unit,person);
        Check.That(person.Profile?.Id==Fixture.ProfileId,"scoped exact transaction read keeps full original mapper");
    }
}
