using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public sealed class ViewColumns<TSource, TView>
{
    private readonly Query<TSource> _query;
    private readonly List<Aliased> _terms = [];
    private readonly List<Node> _joins = [];
    private readonly Dictionary<Type, (Node? Owner, Func<string, string> Col)> _tables;

    internal ViewColumns(Query<TSource> query)
    {
        _query = query;
        _tables = new() { [typeof(TSource)] = (new TableRef(query.SourceTable), OrmMeta.Get(typeof(TSource)).Col) };
    }

    // A definition is a value, not a use of the query: the aliases and bound values the columns allocate while they run
    // are given back, so defining twice from one query gives two equal definitions and leaves the query as it was.
    internal static ViewColumns<TSource, TView> Define(
        Query<TSource> query, Action<ViewColumns<TSource, TView>> columns)
    {
        var projection = new ViewColumns<TSource, TView>(query);
        var mark = query.State.Mark();
        try
        {
            columns(projection);
        }
        finally
        {
            query.State.Rewind(mark);
        }
        return projection;
    }

    public ViewColumns<TSource, TView> Join<TJoined, TOn>(Expression<Func<TOn, TJoined, bool>> on)
    {
        ArgumentNullException.ThrowIfNull(on);
        var meta = OrmMeta.Get(typeof(TJoined));
        var mark = _query.State.AliasMark;
        var alias = _query.State.NextAlias();
        var scope = _query.State.Scope;
        scope[on.Parameters[0]] = Table<TOn>();
        scope[on.Parameters[1]] = (new AliasRef(alias), meta.Col);
        var condition = _query.State.Scoped(on.Parameters, mark, () => _query.Translator.Translate(on.Body));
        _tables[typeof(TJoined)] = (new AliasRef(alias), meta.Col);
        var live = _query.State.LiveTermOf(typeof(TJoined), alias, meta.Col);
        _joins.Add(new InnerJoin(new TableRef(meta.Table, alias), live is null ? condition : new And(condition, live)));
        return this;
    }

    public ViewColumns<TSource, TView> Column(Expression<Func<TView, object?>> target, Expression<Func<TSource, object?>> source) =>
        Column<TSource>(target, source);

    public ViewColumns<TSource, TView> Column<TFrom>(Expression<Func<TView, object?>> target, Expression<Func<TFrom, object?>> source) =>
        Add(target, ColumnOf(source));

    public ViewColumns<TSource, TView> Count(Expression<Func<TView, object?>> target) =>
        Add(target, new Call(Fn.Count, [new Star()]));

    public ViewColumns<TSource, TView> Sum(Expression<Func<TView, object?>> target, Expression<Func<TSource, object?>> source) =>
        Add(target, new Aggregate(PredicateTranslator.IsDecimal(source.Body) ? AggregateFn.DecimalSumOrZero : AggregateFn.SumOrZero, ColumnOf(source)));

    public ViewColumns<TSource, TView> Min(Expression<Func<TView, object?>> target, Expression<Func<TSource, object?>> source) =>
        Add(target, ColumnExtremes.Min(source.Body, ColumnOf(source)));

    internal Select Build()
    {
        var expected = OrmMeta.Get(typeof(TView)).Columns;
        var given = _terms.Select(t => t.Alias).ToList();
        var missing = expected.Where(c => !given.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(typeof(TView).Name + " has columns the definition does not fill: " + string.Join(", ", missing) + ".");
        return _query.BuildView(_terms, _joins.Count == 0 ? null : NodeList.Of(_joins));
    }

    private Node ColumnOf<TFrom>(Expression<Func<TFrom, object?>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = _query.State;
        state.Scope[source.Parameters[0]] = Table<TFrom>();
        return state.Scoped(source.Parameters, state.AliasMark, () => _query.Translator.ColumnOf(source.Body));
    }

    private (Node? Owner, Func<string, string> Col) Table<TTable>() =>
        _tables.TryGetValue(typeof(TTable), out var table)
            ? table
            : throw new InvalidOperationException(typeof(TTable).Name + " is not joined into this view definition.");

    private ViewColumns<TSource, TView> Add(Expression<Func<TView, object?>> target, Node term)
    {
        ArgumentNullException.ThrowIfNull(target);
        var body = target.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : target.Body;
        var column = OrmMeta.Get(typeof(TView)).Col(((MemberExpression)body).Member.Name);
        if (_terms.Any(t => t.Alias == column))
            throw new InvalidOperationException("the definition of " + typeof(TView).Name + " fills " + column + " twice.");
        _terms.Add(new Aliased(term, column));
        return this;
    }
}
