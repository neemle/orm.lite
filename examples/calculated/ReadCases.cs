using Neemle.OrmLite.Generated;

namespace CalculatedDemo;

internal static class ReadCases
{
    internal static async Task RunAsync(Fixture f)
    {
        foreach (var row in new[] { (1, (string?)"MiXeD"), (2, (string?)null), (3, (string?)"") })
            await NoteOrm.InsertAsync(f.Db, new Note { Id = Fixture.Id(row.Item1), Label = row.Item2,
                LowerLabel = "CLIENT VALUE IS NOT STORED" });
        var before = f.Counter.Count;
        var rows = await NoteOrm.Query(f.Db).OrderBy(n => n.Id).ToListAsync();
        Results.Require(f.Counter.Count == before + 1, "full entity read must use one statement");
        var values = string.Join("|", rows.Select(n => Results.Text(n.LowerLabel)));
        Results.Require(values == "mixed|<null>|<empty>", "SQL Lower/null/empty/type parity");
        Results.Case("list", values);
        var one = await NoteOrm.GetAsync(f.Db, Fixture.Id(1));
        Results.Require(one?.LowerLabel == "mixed", "keyed calculated read");
        Results.Require(await NoteOrm.GetAsync(f.Db, Guid.Empty) is null, "missing key remains null");
        Results.Case("keyed", one!.LowerLabel!);
        var firstId = Fixture.Id(1);
        var projected = await NoteOrm.Query(f.Db).Where(n => n.Id == firstId).OrderBy(n => n.Label)
            .ProjectAsync([n => n.Label], r => r.GetString(0));
        Results.Require(projected.SequenceEqual(new[] { "MiXeD" }), "physical filter/order/projection compatible");
        Results.Case("physical-query", projected.Single());
    }
}
