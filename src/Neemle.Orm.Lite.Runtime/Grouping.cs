#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed record GroupingHost(
        PredicateTranslator Translator,
        RenderState State,
        StatementExecutor Exec,
        ISqlProvider Provider,
        string Table,
        Func<Where?> Where,
        Action RefuseClauses);

    internal static class GroupValue
    {
        internal static TValue Read<TValue>(DbDataReader reader, int ordinal)
        {
            if (!reader.IsDBNull(ordinal)) return reader.GetFieldValue<TValue>(ordinal);
            if (default(TValue) is not null)
                throw new InvalidCastException(
                    $"column {ordinal} is NULL and {typeof(TValue).Name} cannot hold NULL; ask for {typeof(TValue).Name}? instead.");
            return default!;
        }

        internal static Node Average(Node column) => new Cast(new Aggregate(AggregateFn.Avg, column), CastType.Double);

        internal static Node DecimalAverage(Node column, int scale)
        {
            if (scale < 0 || scale > EntityDescriptor.MaxDecimalPrecision)
                throw new ArgumentOutOfRangeException(nameof(scale), scale, $"the scale of a decimal average is 0 to {EntityDescriptor.MaxDecimalPrecision}.");
            return new Aggregate(AggregateFn.DecimalAvg, column, scale);
        }

        internal static InvalidOperationException AverageOfDecimal() =>
            new InvalidOperationException("Avg returns a double and would round a decimal column through a double; use AvgDecimal(column, scale), which rounds half away from zero to the scale you name and is exact on both providers.");
    }

    public sealed class AggregateRef<TValue>
    {
        internal AggregateRef(object owner, int index, Node term)
        {
            Owner = owner;
            Index = index;
            Term = term;
        }

        internal object Owner { get; }

        internal int Index { get; }

        internal Node Term { get; }
    }

    public sealed class GroupRow<TKey>
    {
        private readonly object _owner;
        private readonly object?[] _values;

        internal GroupRow(object owner, TKey key, object?[] values)
        {
            _owner = owner;
            Key = key;
            _values = values;
        }

        public TKey Key { get; }

        public TValue Get<TValue>(AggregateRef<TValue> aggregate)
        {
            if (!ReferenceEquals(aggregate.Owner, _owner))
                throw new ArgumentException("the aggregate belongs to a different grouping than this row.", nameof(aggregate));
            return _values[aggregate.Index] is { } value ? (TValue)value : default!;
        }
    }

    public sealed class Grouping<T, TKey>
    {
        private readonly GroupingHost _host;
        private readonly NodeList _keys;
        private readonly string? _enumKey;
        private readonly Func<GroupKeyReader, TKey> _readKey;
        private readonly List<Node> _terms = new List<Node>();
        private readonly List<Func<DbDataReader, object?>> _readers = new List<Func<DbDataReader, object?>>();
        private readonly List<Node> _having = new List<Node>();
        private readonly List<Node> _orders = new List<Node>();
        private int? _limit;
        private int? _offset;

        internal Grouping(GroupingHost host, GroupKeys keys, Func<GroupKeyReader, TKey> readKey)
        {
            _host = host;
            _keys = keys.Nodes;
            _enumKey = keys.EnumKey;
            _readKey = readKey;
        }

        public AggregateRef<long> Count() => Add<long>(new Call(Fn.Count, [new Star()]));

        public AggregateRef<long> CountDistinct(Expression<Func<T, object?>> column) =>
            Add<long>(new Aggregate(AggregateFn.CountDistinct, _host.Translator.ColumnOf(column.Body)));

        [RequiresCapability(Capability.AggregateFilter)]
        public AggregateRef<long> CountWhere(Expression<Func<T, bool>> filter)
        {
            if (!_host.State.Allows(Capability.AggregateFilter, d => d.SupportsAggregateFilter))
                throw _host.State.Refusal(Capability.AggregateFilter, "CountWhere",
                    $"{_host.State.DialectName} has no FILTER aggregate; a second statement would read a different snapshot.");
            return Add<long>(new CountFiltered(
                new Group(_host.Translator.Predicate(filter.Parameters[0], filter.Body))));
        }

        public AggregateRef<TSum> Sum<TSum>(Expression<Func<T, object?>> column) =>
            Add<TSum>(new Aggregate(PredicateTranslator.IsDecimal(column.Body) ? AggregateFn.DecimalSumOrZero : AggregateFn.SumOrZero, _host.Translator.ColumnOf(column.Body)));

        public AggregateRef<TColumn?> Min<TColumn>(Expression<Func<T, object?>> column) =>
            Add<TColumn?>(ColumnExtremes.Min(column.Body, _host.Translator.ColumnOf(column.Body)));

        public AggregateRef<TColumn?> Max<TColumn>(Expression<Func<T, object?>> column) =>
            Add<TColumn?>(ColumnExtremes.Max(column.Body, _host.Translator.ColumnOf(column.Body)));

        public AggregateRef<double?> Avg(Expression<Func<T, object?>> column)
        {
            if (PredicateTranslator.IsDecimal(column.Body)) throw GroupValue.AverageOfDecimal();
            return Add<double?>(GroupValue.Average(_host.Translator.ColumnOf(column.Body)));
        }

        public AggregateRef<decimal?> AvgDecimal(Expression<Func<T, object?>> column, int scale) =>
            Add<decimal?>(GroupValue.DecimalAverage(_host.Translator.ColumnOf(column.Body), scale));

        // The value binds into the parameter list of the query this grouping was made from, on purpose: that list is
        // the one parameter list of every statement the query and its groupings render (BoundParameters, Bind), so
        // the slots are numbered across all of them and a Where added later takes the next slot.
        public Grouping<T, TKey> Having<TValue>(AggregateRef<TValue> aggregate, CompareOp op, TValue value)
        {
            Own(aggregate);
            if (value is null)
                throw new ArgumentNullException(
                    nameof(value), "Having compares the aggregate to a value, and comparing it to NULL matches no group.");
            _having.Add(new Compare(op, aggregate.Term, _host.State.Bind(value)));
            return this;
        }

        public Grouping<T, TKey> OrderByKey(bool descending = false)
        {
            EnumOrdering.RefuseKeyOrder(_enumKey);
            foreach (var key in _keys) _orders.Add(new OrderTerm(key, descending, NullsPlacement.Last));
            return this;
        }

        public Grouping<T, TKey> OrderBy<TValue>(AggregateRef<TValue> aggregate, bool descending = false)
        {
            Own(aggregate);
            _orders.Add(new OrderTerm(aggregate.Term, descending, NullsPlacement.Last));
            return this;
        }

        public Grouping<T, TKey> Take(int n) { _limit = SelectClauses.NonNegative(n, nameof(Take)); return this; }

        public Grouping<T, TKey> Skip(int n) { _offset = SelectClauses.NonNegative(n, nameof(Skip)); return this; }

        public Statement CompileSelect() =>
            StatementCompiler.Compile(BuildSelect(), _host.Provider, StatementKind.Read, _host.Table);

        public string RenderSelect() => CompileSelect().Text;

        internal Select BuildSelect()
        {
            _host.RefuseClauses();
            return Compose();
        }

        private Select Compose() =>
            new Select(
                NodeList.Of(_keys.Concat(_terms)),
                new TableRef(_host.Table),
                _host.Where(),
                _orders.Count > 0 ? new OrderBy(NodeList.Of(_orders)) : null,
                _limit is int limit ? new Limit(limit) : null,
                _offset is int offset ? new Offset(offset) : null,
                GroupBy: _keys,
                Having: _having.Count == 0 ? null : _having.Aggregate((left, right) => new And(left, right)));

        public async Task<List<GroupRow<TKey>>> ToListAsync() =>
            await _host.Exec.RowsAsync(
                CompileSelect(),
                r => new GroupRow<TKey>(
                    this, _readKey(new GroupKeyReader(r, _host.Table)), _readers.Select(read => read(r)).ToArray()));

        private AggregateRef<TValue> Add<TValue>(Node term)
        {
            var ordinal = _keys.Count + _terms.Count;
            _terms.Add(term);
            _readers.Add(r => GroupValue.Read<TValue>(r, ordinal));
            return new AggregateRef<TValue>(this, _terms.Count - 1, term);
        }

        private void Own<TValue>(AggregateRef<TValue> aggregate)
        {
            if (!ReferenceEquals(aggregate.Owner, this))
                throw new ArgumentException("the aggregate belongs to a different grouping.", nameof(aggregate));
        }
    }
