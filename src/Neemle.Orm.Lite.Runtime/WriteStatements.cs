#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neemle.Orm.Lite;

    internal sealed class WriteStatements
    {
        private readonly RenderState _state;
        private readonly ISqlDialect D;
        private readonly SoftDeleteMeta? _softDelete;

        internal WriteStatements(RenderState state, SoftDeleteMeta? softDelete)
        {
            _state = state;
            D = state.Dialect;
            _softDelete = softDelete;
        }

        internal bool RefusesSoftDeletedConflict(OnConflict action, int setCount) =>
            _softDelete is not null && action == OnConflict.DoUpdate && setCount > 0;

        private string? LiveOnlyTargetPredicate(IReadOnlyList<string> conflictColumns) =>
            _softDelete is not null && _softDelete.IsLiveOnlyTarget(conflictColumns)
                ? SoftDeleteSql.LiveTerm(D, _state.Col(_softDelete.Column))
                : null;

        internal (string Sql, List<object?> Parameters) RenderUpsert(
            ResolvedRow row,
            IReadOnlyList<string> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet> sets,
            IReadOnlyList<string> returning)
        {
            ConflictGuard.RequireDefined(action);
            if (!D.SupportsUpsert)
                throw new NotSupportedException(
                    $"{D.GetType().Name} has no upsert; express this as an explicit read-modify-write "
                    + "inside a transaction.");
            ConflictGuard.RequireTarget(conflictColumns, action);
            var ps = new List<object?>();
            var (cols, placeholders) = ValuesAndClock(row, ps);
            var resolved = BoundSets(row.Values, sets, ps);
            var sql = InsertPrefix(cols, placeholders);
            AppendConflict(sql, conflictColumns, action, resolved);
            AppendReturning(sql, returning);
            return (sql.ToString(), ps);
        }

        private (List<string> Cols, List<string> Placeholders) ValuesAndClock(ResolvedRow row, List<object?> ps)
        {
            var values = row.Values;
            var cols = values.Keys.ToList();
            var placeholders = new List<string>();
            foreach (var c2 in cols)
            { placeholders.Add(_state.ValueSlot(values[c2], D.Param(ps.Count))); ps.Add(values[c2]); }
            foreach (var c2 in row.Clock) { cols.Add(c2); placeholders.Add(D.NowExpression); }
            return (cols, placeholders);
        }

        private List<UpsertSet> BoundSets(
            IReadOnlyDictionary<string, object?> values, IReadOnlyList<UpsertSet> sets, List<object?> ps)
        {
            var resolved = new List<UpsertSet>();
            foreach (var s in sets)
            {
                if (s.IncrementBy is not null || s.IsDatabaseClock) { resolved.Add(s); continue; }
                var v = values.TryGetValue(s.Column, out var found) ? found : null;
                if (v is Jsonb) _state.RequireJsonDocumentDialect("bind a JSON document");
                resolved.Add(v is Jsonb
                    ? UpsertSet.ToJsonDocumentParam(s.Column, ps.Count)
                    : UpsertSet.ToParam(s.Column, ps.Count));
                ps.Add(v);
            }
            return resolved;
        }

        private StringBuilder InsertPrefix(List<string> cols, List<string> placeholders) =>
            new StringBuilder()
                .Append("INSERT INTO ").Append(D.Quote(_state.Table))
                .Append(" (").Append(string.Join(", ", cols.Select(D.Quote))).Append(')')
                .Append(" VALUES (").Append(string.Join(", ", placeholders)).Append(')');

        private void AppendConflict(
            StringBuilder sql, IReadOnlyList<string> conflictColumns, OnConflict action, List<UpsertSet> resolved)
        {
            sql.Append(D.ConflictClause(
                conflictColumns, action, resolved, _state.Table, LiveOnlyTargetPredicate(conflictColumns)));
            if (RefusesSoftDeletedConflict(action, resolved.Count))
                sql.Append(" WHERE ").Append(
                    SoftDeleteSql.LiveTerm(D, _state.Col(_softDelete!.Column), D.Quote(_state.Table)));
        }

        private void AppendReturning(StringBuilder sql, IReadOnlyList<string> returning)
        {
            if (returning.Count == 0) return;
            if (!D.SupportsReturning)
                throw new NotSupportedException(
                    $"{D.GetType().Name} cannot return columns from a write; re-read the row instead.");
            sql.Append(D.ReturningClause(returning));
        }

        internal (string Sql, List<object?> Parameters) RenderInsertMany(IReadOnlyList<ResolvedRow> resolvedRows)
        {
            if (resolvedRows.Count == 0)
                throw new InvalidOperationException("InsertManyAsync needs at least one row.");
            var clock = resolvedRows[0].Clock;
            var cols = resolvedRows[0].Values.Keys.ToList();
            var rows = resolvedRows.Select(r => r.Values).ToList();
            var ps = new List<object?>();
            var tuples = new List<string>(rows.Count);
            foreach (var row in rows)
            {
                var slots = new List<string>(cols.Count + clock.Count);
                foreach (var c2 in cols) { slots.Add(_state.ValueSlot(row[c2], D.Param(ps.Count))); ps.Add(row[c2]); }
                foreach (var _ in clock) slots.Add(D.NowExpression);
                tuples.Add("(" + string.Join(", ", slots) + ")");
            }

            var all = cols.Concat(clock);
            var sql = new StringBuilder("INSERT INTO ").Append(D.Quote(_state.Table))
                .Append(" (").Append(string.Join(", ", all.Select(D.Quote))).Append(')')
                .Append(" VALUES ").Append(string.Join(", ", tuples))
                .ToString();
            return (sql, ps);
        }

        internal StringBuilder PhysicalDelete() => new StringBuilder("DELETE FROM ").Append(D.Quote(_state.Table));

        internal string RenderPurgeByKey(IReadOnlyCollection<Guid> ids, Guid? tenantId)
        {
            if (ids is null) throw new ArgumentNullException(nameof(ids));
            if (ids.Count == 0)
                throw new InvalidOperationException(
                    "PurgeAsync requires at least one id (refusing an unfiltered DELETE).");
            var soft = _softDelete
                ?? throw new InvalidOperationException("PurgeAsync by key requires a [SoftDelete] entity.");
            var key = soft.KeyColumn ?? throw new InvalidOperationException(
                "PurgeAsync by key requires the entity's key column in its [SoftDelete] metadata.");
            RequireTenantScope("PurgeAsync", soft, tenantId);
            var sql = PhysicalDelete().Append(" WHERE ")
                .Append(D.AnyOfExpression(D.Quote(key), _state.Slot(_state.Params.Count)));
            AppendTenantTerm(sql, soft);
            return sql.ToString();
        }

        internal string RenderRestore(IReadOnlyCollection<Guid> ids, Guid? tenantId)
        {
            if (ids is null) throw new ArgumentNullException(nameof(ids));
            var soft = _softDelete
                ?? throw new InvalidOperationException("RestoreAsync requires a [SoftDelete] entity.");
            var key = soft.KeyColumn ?? throw new InvalidOperationException(
                "RestoreAsync requires the entity's key column in its [SoftDelete] metadata.");
            RequireTenantScope("RestoreAsync", soft, tenantId);
            var softColumn = D.Quote(_state.Col(soft.Column));
            var sql = new StringBuilder("UPDATE ").Append(D.Quote(_state.Table))
                .Append(" SET ").Append(softColumn).Append(" = NULL WHERE ")
                .Append(D.AnyOfExpression(D.Quote(key), _state.Slot(_state.Params.Count)))
                .Append(" AND ").Append(softColumn).Append(" IS NOT NULL");
            AppendTenantTerm(sql, soft);
            return sql.ToString();
        }

        private static void RequireTenantScope(string verb, SoftDeleteMeta soft, Guid? tenantId)
        {
            if (soft.Platform && tenantId is not null)
                throw new InvalidOperationException($"{verb} on a Platform entity takes no tenant.");
            if (!soft.Platform && (soft.TenantColumn is null || tenantId is null))
                throw new InvalidOperationException(
                    $"{verb} on a tenant entity requires its tenant column and a tenant id.");
        }

        private void AppendTenantTerm(StringBuilder sql, SoftDeleteMeta soft)
        {
            if (!soft.Platform)
                sql.Append(" AND ").Append(D.Quote(soft.TenantColumn!)).Append(" = ")
                    .Append(_state.Slot(_state.Params.Count + 1));
        }
    }
