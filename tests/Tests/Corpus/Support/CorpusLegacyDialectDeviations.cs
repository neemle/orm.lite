namespace Neemle.OrmLite.Tests.Corpus;

// The approved deviations that retiring the legacy dialect path (ISqlDialect, PgDialect, IDdlDialect, PgDdlDialect and
// the dialect parameter of OrmContext) moves in the corpus: the dialect group is deleted with the API it pinned, three
// more cases that exercised only that API are deleted, and the capability refusals now come from the provider path.
// The capture regenerates the goldens and INDEX.md; this list only documents what moved and why.
internal static class CorpusLegacyDialectDeviations
{
    private const string RetiredReason =
        "ORML-21 slice 4: the legacy dialect path is removed from the never-published 0.1.0-pre without a shim; this "
        + "case pinned only that API. The same text is rendered by the provider's serializer, which the Nodes and "
        + "provider rules tests pin.";

    private const string MovedReason =
        "ORML-21 slice 4: the case pinned a helper that is not part of the dialect path; its assertions moved to a "
        + "plain test with the same expected text, and the helper's members are listed as excluded in the coverage "
        + "table.";

    private const string RefusalReason =
        "ORML-21 slice 4: the refusal cases ran against a dialect that supported nothing, which threw a plain "
        + "NotSupportedException worded by the dialect. They now run against a provider that supports nothing, so the "
        + "refusal is the CapabilityNotSupportedException the provider path always threw, naming the provider, the "
        + "capability, the operation and the table, followed by the same guidance sentence as before.";

    private const string ConstantsReason =
        "ORML-21 slice 4: the two Jsonb constants were read only by the removed PostgreSQL dialect, so they are "
        + "removed; the jsonb type and the empty-document default are rendered by the provider's serializer and "
        + "pinned by the ddl-builder entries.";

    private static CorpusIndex.Deviation Retired(string name, string what) =>
        new("dialect/" + name, CorpusIndex.DeviationKind.Removed, what, null, RetiredReason);

    private static CorpusIndex.Deviation Moved(string name, string what) =>
        new("dialect/" + name, CorpusIndex.DeviationKind.Removed, what, null, MovedReason);

    private static CorpusIndex.Deviation RetiredRefusal(string name, string what) =>
        new("refusals/" + name, CorpusIndex.DeviationKind.Removed, what, null, RetiredReason);

    internal static readonly CorpusIndex.Deviation[] All =
    [
        Retired("identifiers-parameters-and-paging", "Quote, Param, Page, MaxBindParameters and PingStatement of PgDialect"),
        Retired("capability-flags", "the nine Supports flags of PgDialect"),
        Retired("fixed-fragments", "NowExpression, the row lock clauses, CountAllExpression and JsonNullValueExpression"),
        Retired("clauses-and-aggregates", "the returning, coalesce, any-of, null-safe, count, sum, min, interval, increment and order members"),
        Retired("conflict-clause", "ConflictClause of PgDialect for every upsert term form"),
        Retired("json-fragments", "the JSON members of PgDialect"),
        Retired("ddl-dialect", "Quote and Literal of PgDdlDialect"),
        Moved("like-pattern", "Escape, EscapeChar, EscapeClause, EscapeLiteral, Contains, StartsWith and EndsWith of LikePattern"),
        Moved("db-extensions-add-param-conversions", "the parameter conversions of AddParam"),
        RetiredRefusal("dialect-json-value-order-without-a-sql-type", "PgDialect refuses a JsonValueOrder it has no SQL type for"),
        RetiredRefusal("dialect-rank-without-options", "PgDialect refuses a rank over no options"),
        new(
            "json/jsonb-default-document-and-column-type",
            CorpusIndex.DeviationKind.Removed,
            "[0] jsonb and [1] '{}'::jsonb, the values of Jsonb.ColumnType and Jsonb.EmptyDocumentDefaultSql",
            null,
            ConstantsReason),
        new(
            "constants/runtime-string-constants",
            CorpusIndex.DeviationKind.Changed,
            "[6] Jsonb.ColumnType = \"jsonb\" and [7] Jsonb.EmptyDocumentDefaultSql = \"'{}'::jsonb\"",
            "(both lines are gone and every later line moves up two places)",
            ConstantsReason),
        new(
            "refusals/capability-* (23 entries) and refusals/group-by-count-where-without-the-filter-capability",
            CorpusIndex.DeviationKind.Changed,
            "type: System.NotSupportedException; message: NoCapabilityDialect has no row lock; ..., the sentence "
            + "after the dialect name differing per capability",
            "type: Neemle.OrmLite.CapabilityNotSupportedException; message: provider 'no-capability' does not "
            + "support RowLock, which ForUpdate requires (table 'corpus_notes'). no-capability has no row lock; ..., "
            + "the same sentence as before",
            RefusalReason),
    ];
}
