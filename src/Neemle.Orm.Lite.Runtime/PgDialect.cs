#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace Neemle.Orm.Lite;

    public sealed class PgDialect : ISqlDialect
    {
        public string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
        public string Param(int index) => "@p" + index;
        public string Page(int? limit, int? offset) =>
            (limit is int l ? " LIMIT " + l : "") + (offset is int o ? " OFFSET " + o : "");

        public int MaxBindParameters => 65535;

        public bool SupportsUpsert => true;
        public bool SupportsReturning => true;

        public string NowExpression => "now()";

        public string CoalesceAssignment(string quotedColumn, string parameter) =>
            quotedColumn + " = COALESCE(" + parameter + ", " + quotedColumn + ")";

        public string ConflictClause(IReadOnlyList<string> conflictColumns, OnConflict action, IReadOnlyList<UpsertSet> sets, string table, string? targetPredicate = null)
        {
            ConflictGuard.RequireDefined(action);
            ConflictGuard.RequireTarget(conflictColumns, action);
            var target = conflictColumns.Count == 0
                ? " ON CONFLICT"
                : " ON CONFLICT (" + string.Join(", ", conflictColumns.Select(Quote)) + ")";
            if (targetPredicate is not null) target += " WHERE " + targetPredicate;
            if (action == OnConflict.DoNothing || sets.Count == 0) return target + " DO NOTHING";
            var assignments = sets.Select(s => s.IncrementBy is long by
                ? $"{Quote(s.Column)} = {Quote(table)}.{Quote(s.Column)} + {by}"
                : s.IsDatabaseClock
                    ? $"{Quote(s.Column)} = {NowExpression}"
                    : s.IsJsonDocument
                        ? $"{Quote(s.Column)} = {JsonDocumentParameter(Param(s.ParamIndex))}"
                        : $"{Quote(s.Column)} = {Param(s.ParamIndex)}");
            return target + " DO UPDATE SET " + string.Join(", ", assignments);
        }

        public string ReturningClause(IReadOnlyList<string> columns) =>
            columns.Count == 0 ? "" : " RETURNING " + string.Join(", ", columns.Select(Quote));

        public bool SupportsArrayParameter => true;

        public bool SupportsNullSafeEquality => true;

        public bool SupportsRowLock => true;

        public bool SupportsAggregateFilter => true;

        public string AnyOfExpression(string quotedColumn, string parameter) =>
            quotedColumn + " = ANY(" + parameter + ")";

        public string NullSafeEqualsExpression(string quotedColumn, string parameter) =>
            quotedColumn + " IS NOT DISTINCT FROM " + parameter;

        public string RowLockClause => " FOR UPDATE";

        public bool SupportsKeyPreservingRowLock => true;

        public string KeyPreservingRowLockClause => " FOR NO KEY UPDATE";

        public string CountAllExpression => "count(*)";

        public string CountFilterExpression(string predicate) =>
            "count(*) FILTER (WHERE " + predicate + ")";

        public string CountDistinctExpression(string quotedColumn) =>
            "count(DISTINCT " + quotedColumn + ")";

        public string SumCoalescedExpression(string quotedColumn) =>
            "COALESCE(SUM(COALESCE(" + quotedColumn + ", 0)), 0)";

        public string MinExpression(string quotedColumn) =>
            "min(" + quotedColumn + ")";

        public bool SupportsIntervalArithmetic => true;

        public string NowPlusMinutesExpression(string parameter) =>
            NowExpression + " + make_interval(mins => " + parameter + ")";

        public string IncrementAssignment(string quotedColumn, string parameter) =>
            quotedColumn + " = COALESCE(" + quotedColumn + ", 0) + " + parameter;

        public string PingStatement => "SELECT 1";

        public bool SupportsJsonDocument => true;

        public string JsonDocumentParameter(string parameter) => parameter + "::jsonb";

        public string JsonNullValueExpression => "NULL::text";

        public string JsonObjectAssignment(IReadOnlyList<(string KeyParameter, string ValueExpression)> members) =>
            "jsonb_build_object(" + string.Join(", ", members.Select(
                m => m.KeyParameter + "::text, " + m.ValueExpression)) + ")";

        public string JsonDocumentMergeAssignment(string quotedDocumentColumn, string objectExpression) =>
            quotedDocumentColumn + " = COALESCE(" + quotedDocumentColumn + ", " + Jsonb.EmptyDocumentDefaultSql
                + ") || " + objectExpression;

        public string JsonDocumentRemoveThenMergeAssignment(
            string quotedDocumentColumn, IReadOnlyList<string> keyParameters, string objectExpression)
        {
            var removed = "COALESCE(" + quotedDocumentColumn + ", " + Jsonb.EmptyDocumentDefaultSql + ")";
            foreach (var key in keyParameters) removed = "(" + removed + " - " + key + "::text)";
            return quotedDocumentColumn + " = " + removed + " || " + objectExpression;
        }

        public string JsonMemberIncrementExpression(string quotedDocumentColumn, string keyParameter, string parameter) =>
            "COALESCE(NULLIF(" + JsonKeyTextExpression(quotedDocumentColumn, keyParameter)
                + ", '')::int, 0) + " + parameter;

        public string JsonKeyTextExpression(string quotedColumn, string keyParameter) =>
            "(" + quotedColumn + " ->> " + keyParameter + ")";

        private static string SqlTypeOf(JsonValueOrder order) => order switch
        {
            JsonValueOrder.Number => "numeric",
            JsonValueOrder.Boolean => "boolean",
            _ => throw new NotSupportedException(
                $"PgDialect renders no SQL type for JsonValueOrder.{order}; Text is compared as the "
                + "extracted text and never reaches this switch, so an arrival here is a new member of "
                + "the enum that this dialect was not given an arm for."),
        };

        private static string ComparedAs(string textExpression, JsonValueOrder order)
        {
            if (order == JsonValueOrder.Text) return textExpression;
            var type = SqlTypeOf(order);
            return "(CASE WHEN pg_input_is_valid(" + textExpression + ", '" + type + "') THEN "
                + textExpression + " END)::" + type;
        }

        public string JsonKeyComparableExpression(string quotedColumn, string keyParameter, JsonValueOrder order) =>
            ComparedAs(JsonKeyTextExpression(quotedColumn, keyParameter), order);

        public string ComparableParameter(string parameter, JsonValueOrder order) =>
            ComparedAs(parameter, order);

        public string JsonKeyRankExpression(
            string quotedColumn, string keyParameter, IReadOnlyList<string> arrangedParameters)
        {
            if (arrangedParameters.Count == 0)
                throw new ArgumentException(
                    "an arrangement with no options ranks every row NULL, which orders nothing; "
                    + "Query<T> compares the extracted value instead of calling this member.",
                    nameof(arrangedParameters));

            return "array_position(ARRAY[" + string.Join(", ", arrangedParameters) + "]::text[], "
                + JsonKeyTextExpression(quotedColumn, keyParameter) + ")";
        }

        public string JsonMemberRemovedAssignment(string quotedDocumentColumn, string documentKey) =>
            quotedDocumentColumn + " = " + quotedDocumentColumn + " - " + Literal(documentKey);

        public string JsonKeyExistsExpression(string quotedColumn, string keyParameter) =>
            "jsonb_exists(" + quotedColumn + ", " + keyParameter + ")";

        private static string JsonKeyValueExpression(string quotedColumn, string keyOperand) =>
            "(" + quotedColumn + " -> " + keyOperand + ")";

        public string JsonKeyIndexTextExpression(string quotedColumn, string literalKey) =>
            "(" + JsonKeyTextExpression(quotedColumn, literalKey) + ")";

        public string JsonKeyIndexValueExpression(string quotedColumn, string literalKey) =>
            "(" + JsonKeyValueExpression(quotedColumn, literalKey) + ")";

        public string JsonArrayContainsStringExpression(string quotedColumn, string keyParameter, string valueParameter)
        {
            var extracted = JsonKeyValueExpression(quotedColumn, keyParameter);
            return "(jsonb_typeof(" + extracted + ") = 'array' AND jsonb_exists(" + extracted + ", " + valueParameter + "))";
        }

        public string JsonArrayContainsAnyExpression(
            string quotedColumn, string keyParameter, IReadOnlyList<string> valueParameters)
        {
            var extracted = JsonKeyValueExpression(quotedColumn, keyParameter);
            var contained = valueParameters.Select(p => extracted + " @> to_jsonb(" + TextValueExpression(p) + ")");
            return "(jsonb_typeof(" + extracted + ") = 'array' AND (" + string.Join(" OR ", contained) + "))";
        }

        public string MovedFieldResolvedTextExpression(string quotedDocumentColumn, string documentKey) =>
            JsonKeyTextExpression(quotedDocumentColumn, Literal(documentKey));

        private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

        public string TextValueExpression(string parameter) => "CAST(" + parameter + " AS text)";

        public string JsonKeyOrderTerm(string quotedColumn, string keyParameter, bool descending, bool nullsFirst) =>
            JsonKeyTextExpression(quotedColumn, keyParameter)
            + OrderDirectionClause(descending, nullsFirst);

        public string MovedFieldNumberOrEpochMillisOrderTerm(
            string quotedDocumentColumn, string keyParameter, string quotedTimestampColumn)
        {
            var text = JsonKeyTextExpression(quotedDocumentColumn, keyParameter);
            return "COALESCE((CASE WHEN pg_input_is_valid(" + text + ", 'double precision') THEN " + text
                + " END)::double precision, FLOOR(EXTRACT(EPOCH FROM " + quotedTimestampColumn
                + ") * 1000)::double precision) ASC";
        }

        public string OrderDirectionClause(bool descending, bool nullsFirst) =>
            (descending ? " DESC" : " ASC") + (nullsFirst ? " NULLS FIRST" : " NULLS LAST");
    }
