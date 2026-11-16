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

    public interface ISqlDialect
    {
        string Quote(string identifier);
        string Param(int index);
        string Page(int? limit, int? offset);

        int MaxBindParameters { get; }

        bool SupportsUpsert { get; }
        bool SupportsReturning { get; }

        string NowExpression { get; }

        string ConflictClause(IReadOnlyList<string> conflictColumns, OnConflict action, IReadOnlyList<UpsertSet> sets, string table, string? targetPredicate = null);

        string ReturningClause(IReadOnlyList<string> columns);

        string CoalesceAssignment(string quotedColumn, string parameter);

        bool SupportsArrayParameter { get; }

        bool SupportsNullSafeEquality { get; }

        bool SupportsRowLock { get; }

        bool SupportsAggregateFilter { get; }

        string AnyOfExpression(string quotedColumn, string parameter);

        string NullSafeEqualsExpression(string quotedColumn, string parameter);

        string RowLockClause { get; }

        bool SupportsKeyPreservingRowLock { get; }

        string KeyPreservingRowLockClause { get; }

        string CountAllExpression { get; }

        string CountFilterExpression(string predicate);

        string CountDistinctExpression(string quotedColumn);

        string SumCoalescedExpression(string quotedColumn);

        string MinExpression(string quotedColumn);

        bool SupportsIntervalArithmetic { get; }

        string NowPlusMinutesExpression(string parameter);

        string IncrementAssignment(string quotedColumn, string parameter);

        string PingStatement { get; }

        bool SupportsJsonDocument { get; }

        string JsonDocumentParameter(string parameter);

        string JsonKeyTextExpression(string quotedColumn, string keyParameter);

        string JsonKeyComparableExpression(string quotedColumn, string keyParameter, JsonValueOrder order);

        string ComparableParameter(string parameter, JsonValueOrder order);

        string JsonKeyRankExpression(
            string quotedColumn, string keyParameter, IReadOnlyList<string> arrangedParameters);

        string JsonKeyExistsExpression(string quotedColumn, string keyParameter);

        string JsonArrayContainsStringExpression(string quotedColumn, string keyParameter, string valueParameter);

        string JsonArrayContainsAnyExpression(string quotedColumn, string keyParameter, IReadOnlyList<string> valueParameters);

        string JsonKeyIndexTextExpression(string quotedColumn, string literalKey);

        string JsonKeyIndexValueExpression(string quotedColumn, string literalKey);

        string MovedFieldResolvedTextExpression(string quotedDocumentColumn, string documentKey);

        string TextValueExpression(string parameter);

        string JsonNullValueExpression { get; }

        string JsonObjectAssignment(IReadOnlyList<(string KeyParameter, string ValueExpression)> members);

        string JsonDocumentMergeAssignment(string quotedDocumentColumn, string objectExpression);

        string JsonDocumentRemoveThenMergeAssignment(
            string quotedDocumentColumn, IReadOnlyList<string> keyParameters, string objectExpression);

        string JsonMemberIncrementExpression(string quotedDocumentColumn, string keyParameter, string parameter);

        string JsonKeyOrderTerm(string quotedColumn, string keyParameter, bool descending, bool nullsFirst);

        string MovedFieldNumberOrEpochMillisOrderTerm(
            string quotedDocumentColumn, string keyParameter, string quotedTimestampColumn);

        string OrderDirectionClause(bool descending, bool nullsFirst);
    }
