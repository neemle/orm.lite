using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace OneToOneDemo;

internal static class DirectionCases
{
    internal static readonly Guid First = new("00000000-0000-0000-0000-000000000001");
    internal static async Task RunAsync(Fixture f)
    {
        foreach (var id in new[] { 1, 2, 3 }) await PersonOrm.InsertAsync(f.Db, new Person { Id = id, Name = "ALICE" });
        await ProfileOrm.InsertAsync(f.Db, new Profile { Id = First, PrincipalId = 1, Label = "one" });
        var profile = (await ProfileOrm.GetAsync(f.Db, First))!;
        var person = (await PersonOrm.GetAsync(f.Db, 1))!;
        await using var connection = await f.Db.DataSource.OpenConnectionAsync();
        var before = f.Counter.Count;
        await ProfileOrm.LoadPersonAsync(f.Db, connection, profile);
        Results.Require(f.Counter.Count == before + 1 && profile.Person?.Id == 1
            && profile.Person.LowerName == "alice", "owner direction calculated full target");
        before = f.Counter.Count;
        await PersonOrm.LoadProfileAsync(f.Db, connection, person);
        Results.Require(f.Counter.Count == before + 1 && person.Profile?.Id == First, "inverse typed principal key");
        var missing = (await PersonOrm.GetAsync(f.Db, 2))!;
        await PersonOrm.LoadProfileAsync(f.Db, connection, missing);
        Results.Require(missing.Profile is null, "principal may have no dependent");
        await using var unit = await UnitOfWork.BeginAsync(f.Db);
        await ProfileOrm.LoadPersonAsync(f.Db, unit, profile);
        Results.Require(profile.Person?.Id == 1 && unit.Connection.State == System.Data.ConnectionState.Open,
            "borrowed unit remains open");
        Results.Case("directions", "custom FK|owner and inverse|calculated target|missing|borrowed unit");
    }
}
