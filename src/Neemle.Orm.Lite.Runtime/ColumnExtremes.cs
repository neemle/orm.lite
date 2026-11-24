#nullable enable
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    // The minimum or the maximum of a column as one aggregate term, for a query, a grouping and a view column alike.
    // Over an enum column the database would answer with the alphabetical first or last member NAME, so it is refused.
    internal static class ColumnExtremes
    {
        internal static Node Min(Expression source, Node column) =>
            Of(source, column, "Min", AggregateFn.Min, AggregateFn.DecimalMin);

        internal static Node Max(Expression source, Node column) =>
            Of(source, column, "Max", AggregateFn.Max, AggregateFn.DecimalMax);

        private static Node Of(Expression source, Node column, string name, AggregateFn plain, AggregateFn exact)
        {
            EnumOrdering.RefuseExtreme(source, name);
            return new Aggregate(PredicateTranslator.IsDecimal(source) ? exact : plain, column);
        }
    }
