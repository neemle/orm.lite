using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class AdditionalGuardCases
{
    internal static async Task RunAsync(Fixture f)
    {
        await MaskedNullAsync(f);
        await RawProjectionAsync(f);
        var before = f.Counter.Count;
        var unknown = new Profile();
        for (var i = 0; i < 2; i++)
            await GuardCases.RefuseAsync(() => CheckAsync(unknown), OneToOneError.UnknownProvenance);
        Results.Require(before == f.Counter.Count, "direct repeated guard cannot perform SQL or promote trust");
        Results.Case("guard-precedence", "masked NULL|conditional|raw Map|projection|repeated Require:zero SQL");
    }

    private static Task CheckAsync(Profile profile)
    {
        OneToOneReadGuard.Require(profile, nameof(Profile.Person));
        return Task.CompletedTask;
    }

    private static async Task MaskedNullAsync(Fixture f)
    {
        var masked = (await ProfileOrm.Query(f.Db).Where(p => p.Id == DirectionCases.First)
            .Redact(p => p.PrincipalId).ToListAsync()).Single();
        var conditional = (await ProfileOrm.Query(f.Db).Where(p => p.Id == DirectionCases.First)
            .RedactWhen(p => p.PrincipalId, p => p.Label == "never").ToListAsync()).Single();
        var old = new Person { Id = 123 }; masked.Person = old;
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var before = f.Counter.Count;
        await GuardCases.RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, masked),
            OneToOneError.UnsupportedVisibility);
        await GuardCases.RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, conditional),
            OneToOneError.UnsupportedVisibility);
        Results.Require(masked.PrincipalId is null && ReferenceEquals(old, masked.Person)
            && before == f.Counter.Count, "masked NULL refuses before shortcut, old navigation unchanged");
    }

    private static async Task RawProjectionAsync(Fixture f)
    {
        var projected = (await ProfileOrm.Query(f.Db).ProjectAsync(
            [p => p.Id, p => p.PrincipalId, p => p.Label], ProfileOrm.Map))[0];
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        Profile raw;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM oto_profiles WHERE principal_id=1";
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("Missing raw source");
            raw = ProfileOrm.Map(reader);
        }
        var before = f.Counter.Count;
        await GuardCases.RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, raw),
            OneToOneError.UnknownProvenance);
        await GuardCases.RefuseAsync(() => ProfileOrm.LoadPersonAsync(f.Db, connection, projected),
            OneToOneError.UnknownProvenance);
        Results.Require(before == f.Counter.Count, "raw mapper and projected exact Map do not grant traversal trust");
    }
}
