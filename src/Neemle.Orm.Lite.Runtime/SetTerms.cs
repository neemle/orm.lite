#nullable enable
using System;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    // The assignments of an UPDATE that Query<T>.Set* write: each one resolves the target column, builds the value
    // node (binding a parameter where the value is a literal) and records the pair in the statement's SetClauses.
    internal sealed class SetTerms
    {
        private readonly RenderState _state;
        private readonly PredicateTranslator _translator;
        private readonly SetClauses _sets;

        internal SetTerms(RenderState state, PredicateTranslator translator, SetClauses sets)
        {
            _state = state;
            _translator = translator;
            _sets = sets;
        }

        internal void Value(Expression column, object? value)
        {
            var target = _translator.ColumnNameOf(column);
            _sets.Add(target, _state.BindValue(value));
        }

        internal void Now(Expression column) => _sets.Add(_translator.ColumnNameOf(column), new ClockNow());

        internal void CoalesceValue(Expression column, object? value)
        {
            var target = _translator.ColumnNameOf(column);
            _sets.Add(target, new Coalesce(_state.BindValue(value), new ColumnRef(target)));
        }

        internal void NowPlusMinutes(Expression column, int minutes)
        {
            if (!_state.Allows(Capability.IntervalArithmetic))
                throw _state.Refusal(Capability.IntervalArithmetic, "SetNowPlusMinutes",
                    $"{_state.ProviderName} has no interval arithmetic; computing the timestamp in the caller "
                    + "would move the lease clock off the server that compares it.");
            _sets.Add(_translator.ColumnNameOf(column), new ClockPlusMinutes(_state.Bind(minutes)));
        }

        internal void Increment(Expression column, int amount)
        {
            var target = _translator.ColumnNameOf(column);
            _sets.Add(target, new Plus(new Coalesce(new ColumnRef(target), new IntLit(0)), _state.Bind(amount)));
        }

        internal void FromColumn(Expression column, Expression source)
        {
            var target = _translator.ColumnNameOf(column);
            _sets.Add(target, new ColumnRef(_translator.ColumnNameOf(source)));
        }

        internal void FromPredicate(Expression column, LambdaExpression value)
        {
            var target = _translator.ColumnNameOf(column);
            _sets.Add(target, new Group(_translator.Predicate(value.Parameters[0], value.Body)));
        }
    }
