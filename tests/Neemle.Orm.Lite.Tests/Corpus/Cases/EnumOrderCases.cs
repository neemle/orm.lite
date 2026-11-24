namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

// ORML-11 (B10): a range or an ordering over an enum column is refused by name. Equality on an enum column is
// recorded by the existing query cases and does not move.
internal static class EnumOrderCases
{
    private const string G = "refusals";

    public static void Register(CorpusRun run)
    {
        run.Rec.Refusal(G, "predicate-range-over-an-enum-column",
            () => run.Q<CorpusNote>().Where(n => n.Status >= CorpusStatus.Active));
        run.Rec.Refusal(G, "predicate-range-over-an-enum-column-as-integers",
            () => run.Q<CorpusNote>().Where(n => (int)n.Status > 1));
        run.Rec.Refusal(G, "predicate-range-over-a-nullable-enum-column-constant-on-the-left",
            () => run.Q<CorpusNote>().Where(n => CorpusStatus.Active < n.Review));
        run.Rec.Refusal(G, "order-by-an-enum-column",
            () => run.Q<CorpusNote>().OrderBy(n => n.Status));
        run.Rec.Refusal(G, "order-by-descending-a-nullable-enum-column",
            () => run.Q<CorpusNote>().OrderByDescending(n => n.Review));
    }
}
