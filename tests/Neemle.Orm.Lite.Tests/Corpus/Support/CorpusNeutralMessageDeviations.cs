namespace Neemle.Orm.Lite.Tests.Corpus;

// The approved deviation that emptying the coupling allow-list of the provider-neutral sources moves in the corpus:
// one refusal message named a PostgreSQL function and now names the capability. The type of the exception and every
// other word are unchanged. The capture regenerates the golden and INDEX.md; this list only documents what moved.
internal static class CorpusNeutralMessageDeviations
{
    private const string Reason =
        "ORML-21 S6: the message is written in the provider-neutral runtime and named the PostgreSQL function "
        + "jsonb_exists, which is provider SQL text in the core; it now names the JSON array-membership test. The "
        + "exception type, the parameter name and the rest of the sentence are unchanged.";

    internal static readonly CorpusIndex.Deviation[] All =
    [
        new(
            "refusals/predicate-json-array-contains-string-null-value",
            CorpusIndex.DeviationKind.Changed,
            "message: Db.JsonArrayContainsString tests for exact-string array membership, and a null value would "
            + "bind SQL NULL -- jsonb_exists against a NULL argument matches no row at all rather than refusing. "
            + "(Parameter 'value')",
            "message: Db.JsonArrayContainsString tests for exact-string array membership, and a null value would "
            + "bind SQL NULL -- a JSON array-membership test against a NULL argument matches no row at all rather "
            + "than refusing. (Parameter 'value')",
            Reason),
    ];
}
