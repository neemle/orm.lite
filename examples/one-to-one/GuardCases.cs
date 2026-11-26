using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class GuardCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var raw = new Profile();
        var before = f.Counter.Count;
        await RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, raw), OneToOneError.UnknownProvenance);
        Results.Require(f.Counter.Count == before, "unknown null refuses before shortcut and SQL");
        var masked = (await PersonOrm.Query(f.Db).Redact(p => p.Note).ToListAsync())[0];
        before = f.Counter.Count;
        await RefuseAsync(() => PersonOrm.LoadProfileAsync(f.Db, connection, masked),
            OneToOneError.UnsupportedVisibility);
        Results.Require(f.Counter.Count == before, "unrelated mask refuses relation SQL");
        var custom = (await new Query<Profile>(f.Db, r => ProfileOrm.Map(r)).ToListAsync())[0];
        before = f.Counter.Count;
        await RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, custom), OneToOneError.UnknownProvenance);
        Results.Require(f.Counter.Count == before, "custom mapper does not promote provenance");
        Results.Case("guards", "unknown null|masked|custom mapper:zero SQL");
    }

    internal static async Task RefuseAsync(Func<Task> action, OneToOneError expected)
    {
        try { await action(); }
        catch (OneToOneDeclarationException error)
        { Results.Require(error.Error == expected, "exact safe refusal"); return; }
        throw new InvalidOperationException("Expected one-to-one refusal");
    }
}
