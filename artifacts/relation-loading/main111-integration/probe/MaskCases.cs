using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
namespace ComposeProbe;
internal static class MaskCases
{
    internal static async Task Run(Fixture f)
    {
        await using var c=await f.Db.DataSource.OpenConnectionAsync();
        var person=(await PersonOrm.Query(f.Db).Redact(p=>p.Note).FirstOrDefaultAsync())!;
        var before=f.Counter.Count;
        await Check.Refuse(()=>PersonOrm.LoadProfileAsync(f.Db,c,person),OneToOneError.UnsupportedVisibility);
        Check.That(f.Counter.Count==before,"policy masked source zero lookup");
        var profile=(await ProfileOrm.Query(f.Db).Redact(p=>p.Note).FirstOrDefaultAsync())!;
        before=f.Counter.Count;
        await Check.Refuse(()=>ProfileOrm.LoadPersonAsync(f.Db,c,profile),OneToOneError.UnsupportedVisibility);
        Check.That(f.Counter.Count==before,"plain masked source zero lookup");
        var custom=(await new Query<Profile>(f.Db,r=>ProfileOrm.Map(r)).ToListAsync()).Single();
        before=f.Counter.Count;
        await Check.Refuse(()=>ProfileOrm.LoadPersonAsync(f.Db,c,custom),OneToOneError.UnknownProvenance);
        Check.That(f.Counter.Count==before,"custom mapper wrapper cannot promote");
    }
    internal static async Task Forged(Fixture f)
    {
        var fake=EntityDescriptor.Of("fake","compose_people",true);
        var before=f.Counter.Count;
        try { await OrmExec.GetAsync(f.Db,fake,1,PersonOrm.Map); }
        catch(GraphRequestException e)
        { Check.That(e.Error==GraphRequestError.UnsupportedShape && f.Counter.Count==before,
            "policy route cannot launder a fake descriptor"); return; }
        throw new InvalidOperationException("Expected fake descriptor refusal");
    }
}
