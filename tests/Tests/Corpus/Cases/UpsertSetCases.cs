namespace Neemle.OrmLite.Tests.Corpus.Cases;

// ORML-14 (B16): an OnConflict.DoUpdate that names no UpsertSet used to be written as DO NOTHING, silently, and on a
// soft-delete entity without the live-row guard. It is refused by name; the two goldens that pinned the old text are
// removed (see the approved deviations).
internal static class UpsertSetCases
{
    private const string G = "refusals";

    public static void Register(CorpusRun run)
    {
        var a = CorpusWorld.RowA;
        var note = new Row<CorpusNote>().With(n => n.Id, a).With(n => n.Title, "t").With(n => n.Rank, 1);
        var task = new Row<CorpusTask>().With(t => t.Id, a).With(t => t.TenantId, CorpusWorld.Tenant)
            .With(t => t.Name, "n").With(t => t.Rank, 2);

        run.Rec.Refusal(G, "upsert-update-without-sets",
            () => run.Q<CorpusNote>().RenderUpsert(note, [n => n.Id], OnConflict.DoUpdate, []));
        run.Rec.Refusal(G, "soft-upsert-update-without-sets",
            () => run.Q<CorpusTask>().RenderUpsert(task, [t => t.Id], OnConflict.DoUpdate, []));
    }
}
