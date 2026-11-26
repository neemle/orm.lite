using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
namespace ComposeProbe;
internal static class StreamCases
{
    internal static async Task Run(Fixture f)
    {
        await using var c=await f.Db.DataSource.OpenConnectionAsync();
        await foreach(var profile in ProfileOrm.Query(f.Db).StreamAsync())
        {
            await ProfileOrm.LoadPersonAsync(f.Db,c,profile);
            Check.That(profile.Person?.Id==1,"ordinary stream original mapper retained");
        }
        await foreach(var person in PersonOrm.Query(f.Db).StreamAsync())
        {
            Check.That(person.Group is null,"ordinary policy-model stream adds no eager expansion");
            OneToOneReadGuard.Require(person,"Profile");
        }
        await foreach(var person in PersonOrm.Query(f.Db).Redact(p=>p.Note).StreamAsync())
        {
            var before=f.Counter.Count;
            await Check.Refuse(()=>PersonOrm.LoadProfileAsync(f.Db,c,person),OneToOneError.UnsupportedVisibility);
            Check.That(before==f.Counter.Count,"masked stream source zero lookup");
        }
    }
}
