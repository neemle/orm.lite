#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed class BuiltWrite
    {
        internal BuiltWrite(Node tree, List<object?> bound)
        {
            Tree = tree;
            Bound = bound;
        }

        internal Node Tree { get; }

        internal List<object?> Bound { get; }
    }

    internal sealed class WriteStatements
    {
        private readonly RenderState _state;
        private readonly SoftDeleteMeta? _softDelete;

        internal WriteStatements(RenderState state, SoftDeleteMeta? softDelete)
        {
            _state = state;
            _softDelete = softDelete;
        }

        internal bool RefusesSoftDeletedConflict(OnConflict action, int setCount) =>
            _softDelete is not null && action == OnConflict.DoUpdate && setCount > 0;

        private Node? LiveOnlyTargetPredicate(IReadOnlyList<string> conflictColumns) =>
            _softDelete is not null && _softDelete.IsLiveOnlyTarget(conflictColumns)
                ? new IsNull(new ColumnRef(_state.Col(_softDelete.Column)))
                : null;

        private Node Bound(object? value, List<object?> bound)
        {
            var parameter = new Param(bound.Count, value);
            var node = _state.ValueOf(value, parameter);
            bound.Add(value);
            return node;
        }

        internal BuiltWrite Upsert(
            ResolvedRow row,
            IReadOnlyList<string> conflictColumns,
            OnConflict action,
            IReadOnlyList<UpsertSet> sets,
            IReadOnlyList<string> returning)
        {
            ConflictGuard.RequireDefined(action);
            if (!_state.Allows(Capability.Upsert, d => d.SupportsUpsert))
                throw _state.Refusal(Capability.Upsert, "upsert",
                    $"{_state.DialectName} has no upsert; express this as an explicit read-modify-write "
                    + "inside a transaction.");
            ConflictGuard.RequireTarget(conflictColumns, action);
            UpsertSetGuard.RequireSets(action, sets.Count);
            var bound = new List<object?>();
            var columns = row.Values.Keys.ToList();
            var cells = columns.Select(c => Bound(row.Values[c], bound)).ToList();
            foreach (var clock in row.Clock)
            {
                columns.Add(clock);
                cells.Add(new ClockNow());
            }
            var assignments = ConflictAssignments(row.Values, sets, bound);
            var tree = new Insert(
                new TableRef(_state.Table),
                ColumnList(columns),
                [new ValuesRow(NodeList.Of(cells))],
                ConflictOf(conflictColumns, action, assignments),
                ReturningOf(returning));
            return new BuiltWrite(tree, bound);
        }

        private List<Node> ConflictAssignments(
            IReadOnlyDictionary<string, object?> values, IReadOnlyList<UpsertSet> sets, List<object?> bound)
        {
            var assignments = new List<Node>(sets.Count);
            foreach (var set in sets)
            {
                var target = new ColumnRef(set.Column);
                if (set.IncrementBy is long by)
                    assignments.Add(new Assignment(target, Incremented(set.Column, by)));
                else if (set.IsDatabaseClock)
                    assignments.Add(new Assignment(target, new ClockNow()));
                else
                    assignments.Add(new Assignment(
                        target, Bound(values.TryGetValue(set.Column, out var found) ? found : null, bound)));
            }
            return assignments;
        }

        // A NULL counter counts from zero, as SetIncrement does: NULL + 1 is NULL, so without COALESCE a counter that
        // starts NULL would stay NULL however often the conflict hits.
        private Node Incremented(string column, long by) =>
            new Plus(new Coalesce(new ColumnRef(column, new TableRef(_state.Table)), new IntLit(0)), new IntLit(by));

        private Conflict ConflictOf(IReadOnlyList<string> conflictColumns, OnConflict action, List<Node> assignments)
        {
            var target = ColumnList(conflictColumns);
            var targetWhere = LiveOnlyTargetPredicate(conflictColumns);
            if (action == OnConflict.DoNothing || assignments.Count == 0) return new Conflict(target, targetWhere);
            Node? guard = RefusesSoftDeletedConflict(action, assignments.Count)
                ? new IsNull(new ColumnRef(_state.Col(_softDelete!.Column), new TableRef(_state.Table)))
                : null;
            return new Conflict(target, targetWhere, new ConflictUpdate(NodeList.Of(assignments), guard));
        }

        private static NodeList ColumnList(IEnumerable<string> columns) =>
            NodeList.Of(columns.Select(c => (Node)new ColumnRef(c)));

        internal Returning? ReturningOf(IReadOnlyList<string> returning)
        {
            if (returning.Count == 0) return null;
            if (!_state.Allows(Capability.Returning, d => d.SupportsReturning))
                throw _state.Refusal(Capability.Returning, "a write that returns columns",
                    $"{_state.DialectName} cannot return columns from a write; re-read the row instead.");
            return new Returning(ColumnList(returning));
        }

        internal BuiltWrite InsertMany(IReadOnlyList<ResolvedRow> resolvedRows)
        {
            if (resolvedRows.Count == 0)
                throw new InvalidOperationException("InsertManyAsync needs at least one row.");
            var clock = resolvedRows[0].Clock;
            var columns = resolvedRows[0].Values.Keys.ToList();
            var bound = new List<object?>();
            var rows = new List<Node>(resolvedRows.Count);
            foreach (var resolved in resolvedRows)
            {
                var cells = new List<Node>(columns.Count + clock.Count);
                foreach (var column in columns) cells.Add(Bound(resolved.Values[column], bound));
                foreach (var _ in clock) cells.Add(new ClockNow());
                rows.Add(new ValuesRow(NodeList.Of(cells)));
            }
            var tree = new Insert(
                new TableRef(_state.Table), ColumnList(columns.Concat(clock)), NodeList.Of(rows));
            return new BuiltWrite(tree, bound);
        }

        internal Node PurgeByKey(IReadOnlyCollection<Guid> ids, Guid? tenantId)
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
            return new Delete(new TableRef(_state.Table), new Where(KeyTerms(key, ids, soft, tenantId)));
        }

        internal Node Restore(IReadOnlyCollection<Guid> ids, Guid? tenantId)
        {
            if (ids is null) throw new ArgumentNullException(nameof(ids));
            var soft = _softDelete
                ?? throw new InvalidOperationException("RestoreAsync requires a [SoftDelete] entity.");
            var key = soft.KeyColumn ?? throw new InvalidOperationException(
                "RestoreAsync requires the entity's key column in its [SoftDelete] metadata.");
            RequireTenantScope("RestoreAsync", soft, tenantId);
            var softColumn = new ColumnRef(_state.Col(soft.Column));
            var terms = KeyTerms(key, ids, soft, tenantId, new IsNotNull(softColumn));
            return new Update(
                new TableRef(_state.Table), [new Assignment(softColumn, new NullLit())], new Where(terms));
        }

        private NodeList KeyTerms(
            string key, IReadOnlyCollection<Guid> ids, SoftDeleteMeta soft, Guid? tenantId, Node? between = null)
        {
            var first = _state.ParamBase + _state.Params.Count;
            var terms = new List<Node> { new AnyOf(new ColumnRef(key), new Param(first, ids.ToArray())) };
            if (between is not null) terms.Add(between);
            if (!soft.Platform)
                terms.Add(new Compare(
                    CompareOp.Equal, new ColumnRef(soft.TenantColumn!), new Param(first + 1, tenantId!.Value)));
            return NodeList.Of(terms);
        }

        private static void RequireTenantScope(string verb, SoftDeleteMeta soft, Guid? tenantId)
        {
            if (soft.Platform && tenantId is not null)
                throw new InvalidOperationException($"{verb} on a Platform entity takes no tenant.");
            if (!soft.Platform && (soft.TenantColumn is null || tenantId is null))
                throw new InvalidOperationException(
                    $"{verb} on a tenant entity requires its tenant column and a tenant id.");
        }
    }
