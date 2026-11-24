#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed class JsonClauses
    {
        private readonly RenderState _state;
        private readonly PredicateTranslator _translator;
        private readonly SetClauses _sets;

        internal JsonClauses(RenderState state, PredicateTranslator translator, SetClauses sets)
        {
            _state = state;
            _translator = translator;
            _sets = sets;
        }

        internal Node MovedFieldNumberOrEpochMillisOrderTerm(
            LambdaExpression document, MovedFieldKey key, LambdaExpression timestamp)
        {
            _state.RequireJsonDocumentDialect("order by a moved numeric field with an epoch-milliseconds fallback");
            var documentColumn = _translator.PlainColumnOf(document.Body);
            var timestampColumn = _translator.PlainColumnOf(timestamp.Body);
            var keyParameter = _state.Bind(RenderState.RequireKey(key.Name));
            return new OrderTerm(new MovedNumberOrEpochMillis(documentColumn, keyParameter, timestampColumn), false);
        }

        internal Node KeyTerm(LambdaExpression column, JsonKey key, bool descending, bool nullsFirst)
        {
            _state.RequireJsonDocumentDialect("order by a key inside a JSON document");
            var documentColumn = _translator.PlainColumnOf(column.Body);
            var keyParameter = _state.Bind(key.Name);
            return new OrderTerm(
                new JsonKeyText(documentColumn, keyParameter),
                descending,
                nullsFirst ? NullsPlacement.First : NullsPlacement.Last);
        }

        internal Node KeyOfTerm(
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
            _state.Scope[outer] = (new TableRef(_state.Table), _state.Col);
            _state.Scope[inner] = (new AliasRef(alias), meta.Col);
            _state.Scope[selector] = (new AliasRef(alias), meta.Col);
            var documentColumn = _translator.PlainColumnOf(column.Body);
            var keyParameter = _state.Bind(ordering.Key.Name);
            var keyText = new JsonKeyText(documentColumn, keyParameter);
            Node extracted = arrangement.Count == 0
                ? new Comparable(keyText, order)
                : new OptionRank(ArrangementParameters(arrangement), keyText);
            var predicate = _translator.Translate(correlation.Body);
            _state.Scope.Remove(outer);
            _state.Scope.Remove(inner);
            _state.Scope.Remove(selector);
            return new OrderTerm(
                new ScalarSubquery(_translator.CorrelatedSelect(other, meta.Table, alias, [extracted], predicate)),
                ordering.Descending,
                ordering.NullsFirst ? NullsPlacement.First : NullsPlacement.Last);
        }

        private NodeList ArrangementParameters(IReadOnlyList<string> arrangement)
        {
            var parameters = new List<Node>(arrangement.Count);
            foreach (var value in arrangement)
            {
                if (value is null)
                    throw new ArgumentNullException(
                        nameof(arrangement),
                        "an arranged option value must be a string; a null element binds SQL NULL, "
                        + "which array_position matches against a value-absent row and would rank "
                        + "those rows among the real options instead of at the end.");
                parameters.Add(_state.Bind(value));
            }
            return NodeList.Of(parameters);
        }

        internal void Merge(LambdaExpression document, Jsonb patch)
        {
            if (document is null) throw new ArgumentNullException(nameof(document));
            if (patch is null) throw new ArgumentNullException(nameof(patch));
            _state.RequireJsonDocumentDialect("merge members into a JSON document");
            var target = MergeTarget(document);
            var merged = new JsonConcat(
                new JsonDocumentOrEmpty(new ColumnRef(target)), new Cast(_state.Bind(patch), CastType.Json));
            _sets.AddMirrored(target, merged);
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
            Node remaining = new JsonDocumentOrEmpty(new ColumnRef(target));
            foreach (var key in clearedKeys)
                remaining = new JsonRemoveKey(remaining, _state.Bind(key.Name));
            _sets.AddMirrored(target, new JsonConcat(remaining, new Cast(_state.Bind(patch), CastType.Json)));
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
            var keyParameter = _state.Bind(key.Name);
            var amountParameter = _state.Bind(amount);
            var column = new ColumnRef(target);
            var incremented = new Plus(new JsonIntOrZero(new JsonKeyText(column, keyParameter)), amountParameter);
            _sets.AddMirrored(
                target, new JsonConcat(new JsonDocumentOrEmpty(column), new JsonObjectOf(keyParameter, incremented)));
        }

        private string MergeTarget(LambdaExpression document)
        {
            _sets.RequireNotMirrored();
            var target = _translator.ColumnNameOf(document.Body);
            _sets.RequireUnassigned(target);
            return target;
        }
    }
