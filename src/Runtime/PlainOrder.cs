#nullable enable
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // The term that OrderBy, OrderByDescending, ThenBy, ThenByDescending and the predicate orders add. NULLs come
    // last in both directions: PostgreSQL does that for an ascending order by default and SQLite puts them first, so
    // without a stated placement OrderBy(...).Take(n) read different rows on the two providers. The grouped orderings
    // already say NULLS LAST, so a query and a grouping now agree.
    internal static class PlainOrder
    {
        internal static OrderTerm Of(Node operand, bool descending) =>
            new(operand, descending, NullsPlacement.Last);
    }
