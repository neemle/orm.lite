namespace Neemle.Orm.Lite.Tests.Corpus;

// The approved deviations that the tenant scope of the history statements (ORML-24) and the enum extreme and order
// guards (ORML-83) move in the corpus goldens, and the one corpus case input that was changed. The texts are read
// from the captured goldens; the capture regenerates the goldens and INDEX.md, and this list only documents what
// moved and why.
internal static class CorpusTenantEnumDeviations
{
    private const string TenantReason =
        "ORML-24: the history reads of the revert and the restore filtered on the table and the row key alone, so a "
        + "tenant could read the history, the existence of a version or the restore plan of another tenant's row; "
        + "each now adds the live-row check of the entry's tenant column, AND EXISTS (SELECT 1 FROM \"corpus_tasks\" "
        + "r WHERE r.\"id\" = @id AND r.\"tenant_id\" = @w), and binds the tenant as the new parameter @w "
        + "(Guid 10000000-0000-0000-0000-000000000001). Nothing else on the line moves.";

    private const string LiveCheck =
        " AND EXISTS (SELECT 1 FROM \"corpus_tasks\" r WHERE r.\"id\" = @id AND r.\"tenant_id\" = @w)";

    private const string LatestOld =
        "SELECT op, xact_id, changed_at FROM row_history WHERE source_table = @t AND row_id = @id";

    private const string RevertHead =
        "WITH st(state) AS (SELECT (to_jsonb(live.*) || coalesce((SELECT jsonb_object_agg(x.k, x.v) FROM (SELECT "
        + "DISTINCT ON (e.k) e.k, e.v FROM row_history h CROSS JOIN LATERAL jsonb_each(h.old_values) AS e(k, v) "
        + "WHERE h.source_table = @t AND h.row_id = @id AND h.id >= @k ORDER BY e.k, h.id) x), '{}'::jsonb)) - "
        + "ARRAY[]::text[] FROM \"corpus_projects\" live WHERE live.\"id\" = @id";

    private const string RevertMid =
        " AND EXISTS (SELECT 1 FROM row_history v WHERE v.id = @k AND v.source_table = @t AND v.row_id = @id)) "
        + "UPDATE \"corpus_projects\" AS t SET \"name\" = r.\"name\" FROM st CROSS JOIN LATERAL "
        + "jsonb_populate_record(NULL::\"corpus_projects\", st.state) AS r WHERE t.\"id\" = @id";

    private const string RevertTail = " AND t.\"deleted_at\" IS NULL";

    private const string RevertReason =
        "ORML-24: the revert of an entity whose tenant column is also its key dropped the tenant predicate, while "
        + "its lock kept it; one rule now filters every entry that is not Platform on its tenant column, key or "
        + "not, so the live row read and the update both carry \"id\" = @w. The history read inside the "
        + "statement and the SET list are unchanged.";

    private const string GroupReason =
        "ORML-83: Min, Max and OrderByKey over an enum column are refused by name, because the database would rank "
        + "the stored member names as text; the case grouped by the enum column Status and ordered by that group "
        + "key, so its group key moved to the integer column rank. A change of the corpus case, not of the "
        + "library output: the statement keeps its shape (count, sum, having, order, limit and offset).";

    private const string ConversionReason =
        "A change of the corpus case, not of the library output: the case captured the double 2.7, and (decimal)2.7 "
        + "is runtime dependent (.NET 10 gives 2.7, .NET 11 gives the 28-digit expansion of the binary value), so it "
        + "now captures the exactly representable 2.5. The statement text \"price\" = @p0 is unchanged.";

    private static CorpusIndex.Deviation Executed(string name, string before, string after) =>
        new(name, CorpusIndex.DeviationKind.Changed, before, after, TenantReason);

    private static CorpusIndex.Deviation LatestVersion() =>
        Executed(
            "executed/soft-restore-plan-* (6 goldens: of-a-batched-delete, of-a-single-update-history, "
            + "of-an-unregistered-batch-member, root-already-live, root-deleted-but-not-locked, root-missing; "
            + "the latest-version statement)",
            LatestOld + " ORDER BY id DESC LIMIT 1",
            LatestOld + LiveCheck + " ORDER BY id DESC LIMIT 1");

    private static CorpusIndex.Deviation NewestVersion() =>
        Executed(
            "executed/row-history-revert-newest-version, row-history-revert-newest-version-without-history-is-refused "
            + "(the newest-version statement)",
            "SELECT max(id) FROM row_history WHERE source_table = @t AND row_id = @id",
            "SELECT max(id) FROM row_history WHERE source_table = @t AND row_id = @id" + LiveCheck);

    private static CorpusIndex.Deviation VersionExists() =>
        Executed(
            "executed/row-history-revert-version-exists",
            "SELECT EXISTS (SELECT 1 FROM row_history WHERE id = @k AND source_table = @t AND row_id = @id)",
            "SELECT EXISTS (SELECT 1 FROM row_history WHERE id = @k AND source_table = @t AND row_id = @id"
            + LiveCheck + ")");

    private static CorpusIndex.Deviation TenantIsKeyRevert() =>
        new(
            "row-history/revert-sql-tenant-is-key-entity",
            CorpusIndex.DeviationKind.Changed,
            RevertHead + RevertMid + RevertTail,
            RevertHead + " AND live.\"id\" = @w" + RevertMid + " AND t.\"id\" = @w" + RevertTail,
            RevertReason);

    private static CorpusIndex.Deviation GroupKey() =>
        new(
            "query-read/group-by-having-and-order-by-aggregate",
            CorpusIndex.DeviationKind.Changed,
            "SELECT \"status\", count(*), COALESCE(SUM(COALESCE(\"views\", 0)), 0) FROM \"corpus_notes\" "
            + "GROUP BY \"status\" HAVING (count(*) > @p0 AND COALESCE(SUM(COALESCE(\"views\", 0)), 0) <= @p1) "
            + "ORDER BY count(*) DESC NULLS LAST, \"status\" ASC NULLS LAST LIMIT 5 OFFSET 10",
            "SELECT \"rank\", count(*), COALESCE(SUM(COALESCE(\"views\", 0)), 0) FROM \"corpus_notes\" "
            + "GROUP BY \"rank\" HAVING (count(*) > @p0 AND COALESCE(SUM(COALESCE(\"views\", 0)), 0) <= @p1) "
            + "ORDER BY count(*) DESC NULLS LAST, \"rank\" ASC NULLS LAST LIMIT 5 OFFSET 10",
            GroupReason);

    private static CorpusIndex.Deviation ClosureDoubleToDecimal() =>
        new(
            "query-read/where-closure-double-to-decimal",
            CorpusIndex.DeviationKind.Changed,
            "[0] Decimal 2.7",
            "[0] Decimal 2.5",
            ConversionReason);

    internal static readonly CorpusIndex.Deviation[] All =
    [
        LatestVersion(),
        NewestVersion(),
        VersionExists(),
        TenantIsKeyRevert(),
        GroupKey(),
        ClosureDoubleToDecimal(),
    ];
}
