using System.Linq.Expressions;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

internal sealed class ClauseTarget<T>(
    Expression<Func<T, object?>> order,
    Expression<Func<T, object?>> column,
    Expression<Func<T, bool>> filter,
    Expression<Func<T, bool>> flag) : IClauseTarget where T : class
{
    internal Query<T> New(string provider) =>
        new(ClauseWorld.Context(provider), _ => throw new NotSupportedException("no rows are read here"));

    internal Query<T> Apply(Query<T> query, string clause) => clause switch
    {
        "None" => query,
        "Take" => query.Take(5),
        "Take0" => query.Take(0),
        "Skip" => query.Skip(2),
        "Skip0" => query.Skip(0),
        "TakeSkip" => query.Take(5).Skip(2),
        "OrderBy" => query.OrderBy(order),
        "OrderByDescending" => query.OrderByDescending(order),
        "ThenBy" => query.ThenBy(order),
        "OrderByPredicate" => query.OrderByPredicate(flag),
        "ForUpdate" => query.ForUpdate(),
        "ForNoKeyUpdate" => query.ForNoKeyUpdate(),
        "TakeOrderBy" => query.OrderBy(order).Take(100),
        "TakeSkipOrderBy" => query.Take(5).Skip(2).OrderBy(order),
        _ => throw new ArgumentOutOfRangeException(nameof(clause), clause, "not a clause case"),
    };

    internal Query<T> Shaped(ClauseShape shape)
    {
        var query = New(shape.Provider);
        if (shape.ClauseFirst) query = Apply(query, shape.Clause);
        query = query.Where(filter);
        if (shape.Operation.StartsWith("Update", StringComparison.Ordinal)) query = query.Set(column, 7);
        return shape.ClauseFirst ? query : Apply(query, shape.Clause);
    }

    public Statement Compile(ClauseShape shape)
    {
        var query = Shaped(shape);
        return shape.Operation switch
        {
            "UpdateAsync" => query.CompileUpdate(),
            "DeleteAsync" => query.CompileDelete(),
            "UpdateReturningAsync" => query.CompileUpdateReturning([order]),
            "DeleteReturningAsync" => query.CompileDeleteReturning([order]),
            "PurgeAsync" => query.CompilePurge(),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Operation, "not a write operation"),
        };
    }

    public string Render(ClauseShape shape)
    {
        var query = Shaped(shape);
        return shape.Operation switch
        {
            "UpdateAsync" => query.RenderUpdate(),
            "DeleteAsync" => query.RenderDelete(),
            "UpdateReturningAsync" => query.RenderUpdateReturning([order]),
            "DeleteReturningAsync" => query.RenderDeleteReturning([order]),
            "PurgeAsync" => query.RenderPurge(),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Operation, "not a write operation"),
        };
    }
}
