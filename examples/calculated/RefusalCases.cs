using Neemle.OrmLite.Generated;

namespace CalculatedDemo;

internal static class RefusalCases
{
    internal static async Task RunAsync(Fixture f)
    {
        var before = f.Counter.Count;
        await RefuseAsync<InvalidOperationException>(() =>
            NoteOrm.Query(f.Db).Where(n => n.LowerLabel == "mixed").ToListAsync());
        await RefuseAsync<InvalidOperationException>(() => NoteOrm.Query(f.Db).OrderBy(n => n.LowerLabel).ToListAsync());
        await RefuseAsync<InvalidOperationException>(() =>
            NoteOrm.Query(f.Db).ProjectAsync([n => n.LowerLabel], r => r.GetString(0)));
        await RefuseAsync<NotSupportedException>(() => NoteOrm.Query(f.Db).Redact(n => n.Label).ToListAsync());
        await RefuseAsync<NotSupportedException>(() =>
            NoteOrm.Query(f.Db).RedactWhen(n => n.Label, n => n.Id == Guid.Empty).ToListAsync());
        Results.Require(f.Counter.Count == before, "unsupported uses and redaction must execute zero statements");
        Results.Case("refusals", "filter|order|projection|mask|conditional-mask:zero-SQL");
    }
    private static async Task RefuseAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { Results.Require(true, "expected refusal"); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
