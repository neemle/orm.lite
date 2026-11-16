using System;
using System.Collections.Generic;
using Neemle.Orm.Lite;

namespace Neemle.Orm.Lite.Tests;

public sealed class SqliteDialect : ISqlDialect
{
    public const string RefusalWordingOnlyThisFixturesOwnBodiesUse = "swap-readiness fixture";

    public string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    public string Param(int index) => "@p" + index;
    public string Page(int? limit, int? offset) =>
        (limit is int l ? " LIMIT " + l : "") + (offset is int o ? " OFFSET " + o : "");

    public int MaxBindParameters { get; init; } = 32766;

    public bool SupportsUpsert => false;
    public bool SupportsReturning => false;

    public string NowExpression => "CURRENT_TIMESTAMP";

    public string CoalesceAssignment(string quotedColumn, string parameter) =>
        quotedColumn + " = COALESCE(" + parameter + ", " + quotedColumn + ")";

    public string ConflictClause(IReadOnlyList<string> conflictColumns, OnConflict action, IReadOnlyList<UpsertSet> sets, string table, string? targetPredicate = null) =>
        throw new NotSupportedException("SqliteDialect has no upsert support in this swap-readiness fixture; unreachable because Query<T>.UpsertAsync guards on SupportsUpsert first.");

    public string ReturningClause(IReadOnlyList<string> columns) =>
        throw new NotSupportedException("SqliteDialect has no RETURNING support in this swap-readiness fixture; unreachable because Query<T>.UpsertAsync/UpdateReturningAsync/DeleteReturningAsync guard on SupportsReturning first.");

    public bool SupportsArrayParameter => false;
    public bool SupportsNullSafeEquality => false;
    public bool SupportsRowLock => false;
    public bool SupportsAggregateFilter => false;

    public string AnyOfExpression(string quotedColumn, string parameter) =>
        throw new NotSupportedException("SqliteDialect has no array parameter in this swap-readiness fixture; unreachable because Query<T> guards on SupportsArrayParameter first.");

    public string NullSafeEqualsExpression(string quotedColumn, string parameter) =>
        throw new NotSupportedException("SqliteDialect has no null-safe equality in this swap-readiness fixture; unreachable because Query<T> guards on SupportsNullSafeEquality first.");

    public string RowLockClause =>
        throw new NotSupportedException("SqliteDialect has no row lock in this swap-readiness fixture; unreachable because Query<T>.ForUpdate guards on SupportsRowLock first.");

    public bool SupportsKeyPreservingRowLock => false;

    public string KeyPreservingRowLockClause =>
        throw new NotSupportedException("SqliteDialect has no key-preserving row lock in this swap-readiness fixture; unreachable because Query<T>.ForNoKeyUpdate guards on SupportsKeyPreservingRowLock first.");

    public string CountAllExpression => "count(*)";

    public string CountFilterExpression(string predicate) =>
        throw new NotSupportedException("SqliteDialect has no FILTER aggregate in this swap-readiness fixture; unreachable because Query<T>.AggregateCountWhere guards on SupportsAggregateFilter first.");

    public string CountDistinctExpression(string quotedColumn) => "count(DISTINCT " + quotedColumn + ")";

    public string SumCoalescedExpression(string quotedColumn) =>
        "COALESCE(SUM(COALESCE(" + quotedColumn + ", 0)), 0)";

    public string MinExpression(string quotedColumn) => "min(" + quotedColumn + ")";

    public bool SupportsIntervalArithmetic => false;

    public string NowPlusMinutesExpression(string parameter) =>
        throw new NotSupportedException("SqliteDialect has no interval arithmetic in this swap-readiness fixture; unreachable because Query<T>.SetNowPlusMinutes guards on SupportsIntervalArithmetic first.");

    public string IncrementAssignment(string quotedColumn, string parameter) =>
        quotedColumn + " = COALESCE(" + quotedColumn + ", 0) + " + parameter;

    public string PingStatement => "SELECT 1 FROM sqlite_master LIMIT 1";

    public bool SupportsJsonDocument => false;

    public string JsonDocumentParameter(string parameter) =>
        throw new NotSupportedException("SqliteDialect has no JSON document type in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonKeyTextExpression(string quotedColumn, string keyParameter) =>
        throw new NotSupportedException("SqliteDialect has no jsonb key extraction in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonKeyComparableExpression(string quotedColumn, string keyParameter, JsonValueOrder order) =>
        throw new NotSupportedException("SqliteDialect has no jsonb key comparison in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string ComparableParameter(string parameter, JsonValueOrder order) =>
        throw new NotSupportedException("SqliteDialect has no typed-comparison guard in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonKeyRankExpression(
        string quotedColumn, string keyParameter, IReadOnlyList<string> arrangedParameters) =>
        throw new NotSupportedException("SqliteDialect has no jsonb key ranking in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonKeyExistsExpression(string quotedColumn, string keyParameter) =>
        throw new NotSupportedException("SqliteDialect has no jsonb key existence operator in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonArrayContainsStringExpression(string quotedColumn, string keyParameter, string valueParameter) =>
        throw new NotSupportedException("SqliteDialect has no jsonb array-membership test in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonArrayContainsAnyExpression(string quotedColumn, string keyParameter, IReadOnlyList<string> valueParameters) =>
        throw new NotSupportedException("SqliteDialect has no jsonb array-membership test in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonKeyIndexTextExpression(string quotedColumn, string literalKey) =>
        throw new NotSupportedException("SqliteDialect has no jsonb expression index in this swap-readiness fixture; unreachable because the only caller renders PostgreSQL DDL.");

    public string JsonKeyIndexValueExpression(string quotedColumn, string literalKey) =>
        throw new NotSupportedException("SqliteDialect has no jsonb expression index in this swap-readiness fixture; unreachable because the only caller renders PostgreSQL DDL.");

    public string JsonKeyOrderTerm(string quotedColumn, string keyParameter, bool descending, bool nullsFirst) =>
        throw new NotSupportedException("SqliteDialect has no jsonb ordering in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string MovedFieldResolvedTextExpression(string quotedDocumentColumn, string documentKey) =>
        throw new NotSupportedException("SqliteDialect has no moved-field resolution in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string TextValueExpression(string parameter) =>
        throw new NotSupportedException("SqliteDialect renders no text cast in this swap-readiness fixture; unreachable because it is only ever composed beside MovedFieldResolvedTextExpression, which refuses first.");

    public string JsonNullValueExpression =>
        throw new NotSupportedException("SqliteDialect composes no JSON document in this swap-readiness fixture, so it spells no null inside one; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonObjectAssignment(IReadOnlyList<(string KeyParameter, string ValueExpression)> members) =>
        throw new NotSupportedException("SqliteDialect has no JSON document column type in this swap-readiness fixture, so it composes no document into one; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonDocumentMergeAssignment(string quotedDocumentColumn, string objectExpression) =>
        throw new NotSupportedException("SqliteDialect has no JSON document column type in this swap-readiness fixture, so it merges nothing into one; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonDocumentRemoveThenMergeAssignment(
        string quotedDocumentColumn, IReadOnlyList<string> keyParameters, string objectExpression) =>
        throw new NotSupportedException("SqliteDialect has no JSON document column type in this swap-readiness fixture, so it removes and merges nothing; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string JsonMemberIncrementExpression(string quotedDocumentColumn, string keyParameter, string parameter) =>
        throw new NotSupportedException("SqliteDialect has no JSON document column type in this swap-readiness fixture, so it increments no member of one; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string MovedFieldNumberOrEpochMillisOrderTerm(
        string quotedDocumentColumn, string keyParameter, string quotedTimestampColumn) =>
        throw new NotSupportedException("SqliteDialect has no moved-field ordering in this swap-readiness fixture; unreachable because Query<T> guards on SupportsJsonDocument first.");

    public string OrderDirectionClause(bool descending, bool nullsFirst) =>
        (descending ? " DESC" : " ASC") + (nullsFirst ? " NULLS FIRST" : " NULLS LAST");
}
