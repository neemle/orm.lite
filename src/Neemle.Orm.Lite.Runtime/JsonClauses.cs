#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Neemle.Orm.Lite;

    internal sealed class JsonClauses
    {
        private readonly RenderState _state;
        private readonly PredicateTranslator _translator;
        private readonly SetClauses _sets;
        private readonly ISqlDialect D;
        private readonly List<object?> _params;

        internal JsonClauses(RenderState state, PredicateTranslator translator, SetClauses sets)
        {
            _state = state;
            _translator = translator;
            _sets = sets;
            D = state.Dialect;
            _params = state.Params;
        }

        internal string MovedFieldNumberOrEpochMillisOrderTerm(
            LambdaExpression document, MovedFieldKey key, LambdaExpression timestamp)
        {
            _state.RequireJsonDocumentDialect("order by a moved numeric field with an epoch-milliseconds fallback");
            var quotedDocument = _translator.ColumnOf(document.Body);
            var quotedTimestamp = _translator.ColumnOf(timestamp.Body);
            _params.Add(RenderState.RequireKey(key.Name));
            return D.MovedFieldNumberOrEpochMillisOrderTerm(quotedDocument, _state.NextSlot(), quotedTimestamp);
        }

        internal string OrderTerm(LambdaExpression column, JsonKey key, bool descending, bool nullsFirst)
        {
            _state.RequireJsonDocumentDialect("order by a key inside a JSON document");
            var quoted = _translator.ColumnOf(column.Body);
            _params.Add(key.Name);
            return D.JsonKeyOrderTerm(quoted, _state.NextSlot(), descending, nullsFirst);
        }

        internal string OrderTermOf(
            Type other, LambdaExpression correlation, LambdaExpression column, JsonKeyOrdering ordering)
        {
            if (ordering is null) throw new ArgumentNullException(nameof(ordering));
            var arrangement = ordering.Arrangement;
            var order = ordering.Order;
            _state.RequireJsonDocumentDialect("order by a key inside a JSON document in another table");
            var outer = correlation.Parameters[0];
            var inner = correlation.Parameters[1];
            var meta = OrmMeta.Get(other);
            var alias = _state.NextAlias();
            var selector = column.Parameters[0];
            _state.Scope[outer] = (D.Quote(_state.Table), _state.Col);
            _state.Scope[inner] = (alias, meta.Col);
            _state.Scope[selector] = (alias, meta.Col);
            var quotedDocument = _translator.ColumnOf(column.Body);
            _params.Add(ordering.Key.Name);
            var keySlot = _state.NextSlot();
            var extracted = arrangement.Count == 0
                ? D.JsonKeyComparableExpression(quotedDocument, keySlot, order)
                : D.JsonKeyRankExpression(quotedDocument, keySlot, ArrangementSlots(arrangement));
            var predicate = _translator.Translate(correlation.Body);
            _state.Scope.Remove(outer);
            _state.Scope.Remove(inner);
            _state.Scope.Remove(selector);
            return "(SELECT " + extracted + " FROM " + D.Quote(meta.Table) + " " + alias + " WHERE " + predicate
                + _state.LiveTermOf(other, alias, meta.Col) + ")"
                + D.OrderDirectionClause(ordering.Descending, ordering.NullsFirst);
        }

        private IReadOnlyList<string> ArrangementSlots(IReadOnlyList<string> arrangement)
        {
            var slots = new List<string>(arrangement.Count);
            foreach (var value in arrangement)
            {
                if (value is null)
                    throw new ArgumentNullException(
                        nameof(arrangement),
                        "an arranged option value must be a string; a null element binds SQL NULL, "
                        + "which array_position matches against a value-absent row and would rank "
                        + "those rows among the real options instead of at the end.");
                _params.Add(value);
                slots.Add(_state.NextSlot());
            }
            return slots;
        }

        internal void Merge(LambdaExpression document, Jsonb patch)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (patch is null) throw new ArgumentNullException(nameof(patch));
            _state.RequireJsonDocumentDialect("merge members into a JSON document");
            var target = MergeTarget(document);
            var slot = _state.Slot(_params.Count);
            _params.Add(patch);
            _sets.AddMirrored(target, D.JsonDocumentMergeAssignment(target, D.JsonDocumentParameter(slot)));
        }

        internal void RemoveThenMerge(
            LambdaExpression document,
            IReadOnlyList<MovedFieldKey> clearedKeys,
            IReadOnlyList<MovedFieldKey> setKeys,
            Jsonb patch)
        {
            RequireRemoveThenMergeArguments(document, clearedKeys, setKeys, patch);
            _state.RequireJsonDocumentDialect("remove and merge members in a JSON document");
            var target = MergeTarget(document);
            var keySlots = new List<string>(clearedKeys.Count);
            foreach (var key in clearedKeys)
            {
                _params.Add(key.Name);
                keySlots.Add(_state.NextSlot());
            }
            var patchSlot = _state.Slot(_params.Count);
            _params.Add(patch);
            _sets.AddMirrored(target, D.JsonDocumentRemoveThenMergeAssignment(
                target, keySlots, D.JsonDocumentParameter(patchSlot)));
        }

        private static void RequireRemoveThenMergeArguments(
            LambdaExpression document,
            IReadOnlyList<MovedFieldKey> clearedKeys,
            IReadOnlyList<MovedFieldKey> setKeys,
            Jsonb patch)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (clearedKeys is null) throw new ArgumentNullException(nameof(clearedKeys));
            if (setKeys is null) throw new ArgumentNullException(nameof(setKeys));
            if (patch is null) throw new ArgumentNullException(nameof(patch));
            if (clearedKeys.Count == 0 && setKeys.Count == 0)
                throw new ArgumentException("a JSON remove-then-merge mutation must clear or set at least one member");
            RequireDistinctKeys(clearedKeys, setKeys);
        }

        private static void RequireDistinctKeys(
            IReadOnlyList<MovedFieldKey> clearedKeys, IReadOnlyList<MovedFieldKey> setKeys)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in clearedKeys)
                if (!names.Add(key.Name))
                    throw new ArgumentException($"the JSON clear key `{key.Name}` is duplicated", nameof(clearedKeys));
            foreach (var key in setKeys)
                if (!names.Add(key.Name))
                    throw new ArgumentException(
                        $"the JSON member `{key.Name}` cannot be cleared and set in the same mutation, or set twice",
                        nameof(setKeys));
        }

        internal void MemberIncrement(LambdaExpression document, MovedFieldKey key, int amount)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            _state.RequireJsonDocumentDialect("increment a member of a JSON document");
            var target = MergeTarget(document);
            var keySlot = _state.Slot(_params.Count);
            _params.Add(key.Name);
            var amountSlot = _state.Slot(_params.Count);
            _params.Add(amount);
            var member = D.JsonObjectAssignment(
                [(keySlot, D.JsonMemberIncrementExpression(target, keySlot, amountSlot))]);
            _sets.AddMirrored(target, D.JsonDocumentMergeAssignment(target, member));
        }

        private string MergeTarget(LambdaExpression document)
        {
            _sets.RequireNotMirrored();
            var target = _translator.ColumnOf(document.Body);
            _sets.RequireUnassigned(target);
            return target;
        }
    }
