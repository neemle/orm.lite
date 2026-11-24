#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // The aggregate terms of a query's AggregatesAsync, in the order they were added, and the conversion of the
    // exact (decimal) ones from the provider's answer.
    internal sealed class AggregateClauses
    {
        private readonly RenderState _state;
        private readonly PredicateTranslator _translator;
        private readonly List<Node> _terms = new List<Node>();

        internal AggregateClauses(RenderState state, PredicateTranslator translator)
        {
            _state = state;
            _translator = translator;
        }

        internal int Count => _terms.Count;

        internal NodeList Terms => NodeList.Of(_terms);

        internal void AddCount() => _terms.Add(new Call(Fn.Count, [new Star()]));

        internal void AddCountDistinct(Expression column) =>
            _terms.Add(new Aggregate(AggregateFn.CountDistinct, _translator.ColumnOf(column)));

        internal void AddSum(Expression column)
        {
            var function = PredicateTranslator.IsDecimal(column) ? AggregateFn.DecimalSumOrZero : AggregateFn.SumOrZero;
            _terms.Add(new Aggregate(function, _translator.ColumnOf(column)));
        }

        internal void AddMin(Expression column) => _terms.Add(ColumnExtremes.Min(column, _translator.ColumnOf(column)));

        internal void AddMax(Expression column) => _terms.Add(ColumnExtremes.Max(column, _translator.ColumnOf(column)));

        internal void AddAvg(Expression column)
        {
            if (PredicateTranslator.IsDecimal(column)) throw GroupValue.AverageOfDecimal();
            _terms.Add(GroupValue.Average(_translator.ColumnOf(column)));
        }

        internal void AddAvgDecimal(Expression column, int scale) =>
            _terms.Add(GroupValue.DecimalAverage(_translator.ColumnOf(column), scale));

        internal void AddCountWhere(LambdaExpression filter)
        {
            if (!_state.Allows(Capability.AggregateFilter))
                throw _state.Refusal(Capability.AggregateFilter, "AggregateCountWhere",
                    $"{_state.ProviderName} has no FILTER aggregate; "
                    + "a second statement would read a different snapshot.");
            _terms.Add(new CountFiltered(new Group(_translator.Predicate(filter.Parameters[0], filter.Body))));
        }

        internal object?[] Exact(object?[] row)
        {
            for (var i = 0; i < row.Length; i++)
                if (IsExact(_terms[i]) && row[i] is not null)
                    row[i] = DecimalOf(row[i]);
            return row;
        }

        private static bool IsExact(Node term) =>
            term is Aggregate
            {
                Function: AggregateFn.DecimalSumOrZero or AggregateFn.DecimalMin
                    or AggregateFn.DecimalMax or AggregateFn.DecimalAvg,
            };

        private static decimal DecimalOf(object? value) => value switch
        {
            null or DBNull => 0m,
            string text => decimal.Parse(
                text, ScalarValue.DecimalStyle, System.Globalization.CultureInfo.InvariantCulture),
            _ => System.Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture),
        };
    }
