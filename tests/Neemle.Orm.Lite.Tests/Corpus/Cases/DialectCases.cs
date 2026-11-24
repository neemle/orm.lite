namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class DialectCases
{
    private const string G = "dialect";

    public static void Register(CorpusRun run)
    {
        var rec = run.Rec;
        var pg = new RecordingDialect(rec.Touch);
        ISqlDialect d = pg;
        var direct = new PgDialect();

        string B(bool v) => v ? "true" : "false";

        rec.Text(G, "identifiers-parameters-and-paging", () => CorpusFormat.Lines(
        [
            "Quote(plain) = " + d.Quote("notes"),
            "Quote(embedded quote) = " + d.Quote("we\"ird"),
            "Quote(empty) = " + d.Quote(""),
            "Param(0) = " + d.Param(0),
            "Param(12) = " + d.Param(12),
            "Page(null, null) = " + CorpusFormat.Quote(d.Page(null, null)),
            "Page(5, null) = " + CorpusFormat.Quote(d.Page(5, null)),
            "Page(null, 7) = " + CorpusFormat.Quote(d.Page(null, 7)),
            "Page(5, 7) = " + CorpusFormat.Quote(d.Page(5, 7)),
            "Page(0, 0) = " + CorpusFormat.Quote(d.Page(0, 0)),
            "MaxBindParameters = " + d.MaxBindParameters,
            "PingStatement = " + d.PingStatement,
        ]));

        rec.Text(G, "capability-flags", () => CorpusFormat.Lines(
        [
            "SupportsUpsert = " + B(d.SupportsUpsert),
            "SupportsReturning = " + B(d.SupportsReturning),
            "SupportsArrayParameter = " + B(d.SupportsArrayParameter),
            "SupportsNullSafeEquality = " + B(d.SupportsNullSafeEquality),
            "SupportsRowLock = " + B(d.SupportsRowLock),
            "SupportsKeyPreservingRowLock = " + B(d.SupportsKeyPreservingRowLock),
            "SupportsAggregateFilter = " + B(d.SupportsAggregateFilter),
            "SupportsIntervalArithmetic = " + B(d.SupportsIntervalArithmetic),
            "SupportsJsonDocument = " + B(d.SupportsJsonDocument),
        ]));

        rec.Text(G, "fixed-fragments", () => CorpusFormat.Lines(
        [
            "NowExpression = " + d.NowExpression,
            "RowLockClause = " + CorpusFormat.Quote(d.RowLockClause),
            "KeyPreservingRowLockClause = " + CorpusFormat.Quote(d.KeyPreservingRowLockClause),
            "CountAllExpression = " + d.CountAllExpression,
            "JsonNullValueExpression = " + d.JsonNullValueExpression,
        ]));

        rec.Text(G, "clauses-and-aggregates", () => CorpusFormat.Lines(
        [
            "ReturningClause([]) = " + CorpusFormat.Quote(d.ReturningClause([])),
            "ReturningClause([a, b]) = " + CorpusFormat.Quote(d.ReturningClause(["a", "b\"c"])),
            "CoalesceAssignment = " + d.CoalesceAssignment("\"c\"", "@p0"),
            "AnyOfExpression = " + d.AnyOfExpression("\"c\"", "@p0"),
            "NullSafeEqualsExpression = " + d.NullSafeEqualsExpression("\"c\"", "@p0"),
            "CountFilterExpression = " + d.CountFilterExpression("\"c\" > @p0"),
            "CountDistinctExpression = " + d.CountDistinctExpression("\"c\""),
            "SumCoalescedExpression = " + d.SumCoalescedExpression("\"c\""),
            "MinExpression = " + d.MinExpression("\"c\""),
            "NowPlusMinutesExpression = " + d.NowPlusMinutesExpression("@p0"),
            "IncrementAssignment = " + d.IncrementAssignment("\"c\"", "@p0"),
            "OrderDirectionClause(false,false) = " + CorpusFormat.Quote(d.OrderDirectionClause(false, false)),
            "OrderDirectionClause(true,false) = " + CorpusFormat.Quote(d.OrderDirectionClause(true, false)),
            "OrderDirectionClause(false,true) = " + CorpusFormat.Quote(d.OrderDirectionClause(false, true)),
            "OrderDirectionClause(true,true) = " + CorpusFormat.Quote(d.OrderDirectionClause(true, true)),
        ]));

        rec.Text(G, "conflict-clause", () => CorpusFormat.Lines(
        [
            "untargeted do nothing = " + d.ConflictClause([], OnConflict.DoNothing, [], "t"),
            "targeted do nothing = " + d.ConflictClause(["id"], OnConflict.DoNothing, [], "t"),
            "targeted with predicate = " + d.ConflictClause(["a", "b"], OnConflict.DoNothing, [], "t", "\"deleted_at\" IS NULL"),
            "do update without sets = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [], "t"),
            "do update parameter set = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [UpsertSet.ToParam("title", 3)], "t"),
            "do update json parameter set = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [UpsertSet.ToJsonDocumentParam("doc", 4)], "t"),
            "do update increment = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [UpsertSet.Increment("views", 5)], "t"),
            "do update clock = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [UpsertSet.Now("seen_at")], "t"),
            "do update mixed with predicate = " + d.ConflictClause(["id"], OnConflict.DoUpdate, [UpsertSet.ToParam("a", 0), UpsertSet.Increment("b", -1), UpsertSet.Now("c")], "ta\"ble", "x"),
        ]));

        rec.Text(G, "json-fragments", () => CorpusFormat.Lines(
        [
            "JsonDocumentParameter = " + d.JsonDocumentParameter("@p0"),
            "JsonKeyTextExpression = " + d.JsonKeyTextExpression("\"c\"", "@p0"),
            "JsonKeyComparableExpression(Text) = " + d.JsonKeyComparableExpression("\"c\"", "@p0", JsonValueOrder.Text),
            "JsonKeyComparableExpression(Number) = " + d.JsonKeyComparableExpression("\"c\"", "@p0", JsonValueOrder.Number),
            "JsonKeyComparableExpression(Boolean) = " + d.JsonKeyComparableExpression("\"c\"", "@p0", JsonValueOrder.Boolean),
            "ComparableParameter(Text) = " + d.ComparableParameter("@p1", JsonValueOrder.Text),
            "ComparableParameter(Number) = " + d.ComparableParameter("@p1", JsonValueOrder.Number),
            "ComparableParameter(Boolean) = " + d.ComparableParameter("@p1", JsonValueOrder.Boolean),
            "JsonKeyRankExpression = " + d.JsonKeyRankExpression("\"c\"", "@p0", ["@p1", "@p2"]),
            "JsonKeyExistsExpression = " + d.JsonKeyExistsExpression("\"c\"", "@p0"),
            "JsonArrayContainsStringExpression = " + d.JsonArrayContainsStringExpression("\"c\"", "@p0", "@p1"),
            "JsonArrayContainsAnyExpression(one) = " + d.JsonArrayContainsAnyExpression("\"c\"", "@p0", ["@p1"]),
            "JsonArrayContainsAnyExpression(two) = " + d.JsonArrayContainsAnyExpression("\"c\"", "@p0", ["@p1", "@p2"]),
            "JsonKeyIndexTextExpression = " + d.JsonKeyIndexTextExpression("\"c\"", "'k'"),
            "JsonKeyIndexValueExpression = " + d.JsonKeyIndexValueExpression("\"c\"", "'k'"),
            "MovedFieldResolvedTextExpression = " + d.MovedFieldResolvedTextExpression("\"c\"", "it's"),
            "TextValueExpression = " + d.TextValueExpression("@p0"),
            "JsonObjectAssignment(one) = " + d.JsonObjectAssignment([("@p0", "@p1")]),
            "JsonObjectAssignment(two) = " + d.JsonObjectAssignment([("@p0", "@p1"), ("@p2", "NULL::text")]),
            "JsonDocumentMergeAssignment = " + d.JsonDocumentMergeAssignment("\"c\"", "@p0::jsonb"),
            "JsonDocumentRemoveThenMergeAssignment(none) = " + d.JsonDocumentRemoveThenMergeAssignment("\"c\"", [], "@p0::jsonb"),
            "JsonDocumentRemoveThenMergeAssignment(two) = " + d.JsonDocumentRemoveThenMergeAssignment("\"c\"", ["@p0", "@p1"], "@p2::jsonb"),
            "JsonMemberIncrementExpression = " + d.JsonMemberIncrementExpression("\"c\"", "@p0", "@p1"),
            "JsonKeyOrderTerm(asc, nulls last) = " + d.JsonKeyOrderTerm("\"c\"", "@p0", false, false),
            "JsonKeyOrderTerm(desc, nulls first) = " + d.JsonKeyOrderTerm("\"c\"", "@p0", true, true),
            "MovedFieldNumberOrEpochMillisOrderTerm = " + d.MovedFieldNumberOrEpochMillisOrderTerm("\"c\"", "@p0", "\"t\""),
            "PgDialect.JsonMemberRemovedAssignment = " + direct.JsonMemberRemovedAssignment("\"c\"", "it's"),
        ]));

        rec.Text(G, "like-pattern", () => CorpusFormat.Lines(
        [
            "Escape = " + LikePattern.Escape,
            "EscapeChar = " + LikePattern.EscapeChar,
            "EscapeClause = " + LikePattern.EscapeClause,
            "EscapeLiteral(empty) = " + CorpusFormat.Quote(LikePattern.EscapeLiteral("")),
            "EscapeLiteral(plain) = " + CorpusFormat.Quote(LikePattern.EscapeLiteral("plain")),
            "EscapeLiteral(special) = " + CorpusFormat.Quote(LikePattern.EscapeLiteral("a%b_c~d")),
            "Contains = " + CorpusFormat.Quote(LikePattern.Contains("a%b")),
            "StartsWith = " + CorpusFormat.Quote(LikePattern.StartsWith("a_b")),
            "EndsWith = " + CorpusFormat.Quote(LikePattern.EndsWith("a~b")),
        ]));

        rec.Text(G, "ddl-dialect", () =>
        {
            var ddl = new PgDdlDialect();
            return CorpusFormat.Lines(
            [
                "Quote(plain) = " + ddl.Quote("t"),
                "Quote(embedded quote) = " + ddl.Quote("a\"b"),
                "Literal(null) = " + ddl.Literal(null),
                "Literal(string) = " + ddl.Literal("it's"),
                "Literal(true) = " + ddl.Literal(true),
                "Literal(false) = " + ddl.Literal(false),
                "Literal(int) = " + ddl.Literal(42),
                "Literal(long) = " + ddl.Literal(-7L),
                "Literal(double) = " + ddl.Literal(1.5),
                "Literal(decimal) = " + ddl.Literal(2.25m),
                "Literal(guid) = " + ddl.Literal(CorpusWorld.RowA),
            ]);
        });

        rec.Text(G, "db-extensions-add-param-conversions", () =>
        {
            var source = new RecordingDataSource(new RecordingLog(), "dialect");
            using var cmd = source.CreateCommand("SELECT 1");
            cmd.AddParam("n", null);
            cmd.AddParam("e", CorpusStatus.Active);
            cmd.AddParam("j", Jsonb.Parse("{\"a\":1}"));
            cmd.AddParam("s", "text");
            cmd.AddParam("g", CorpusWorld.RowA);
            return CorpusFormat.Lines(cmd.Parameters.Cast<System.Data.Common.DbParameter>().Select(p => p.ParameterName + " = " + CorpusFormat.Value(p.Value)));
        });
    }
}
