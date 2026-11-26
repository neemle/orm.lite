using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class LegacyCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var person = (await PersonOrm.GetAsync(f.Db, 1))!;
        var old = new Profile { Label = "old navigation" };
        person.Profile = old;
        await f.ExecuteAsync("DROP INDEX uq_oto_profile_person");
        await ProfileOrm.InsertAsync(f.Db, new Profile { Id = Guid.NewGuid(), PrincipalId = 1 });
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var before = f.Counter.Count;
        try { await PersonOrm.LoadProfileAsync(f.Db, connection, person); }
        catch (OneToOneCardinalityException)
        {
            Results.Require(ReferenceEquals(old, person.Profile), "duplicate leaves old navigation unchanged");
            Results.Require(f.Counter.Count == before + 1, "bounded duplicate read is one statement");
            Results.Case("legacy", "duplicate refused|old navigation retained|one statement");
            return;
        }
        throw new InvalidOperationException("Legacy duplicate was silently selected");
    }
}
