#nullable enable
using System;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

    // SumAsync returns a long. A column that holds a fraction would be rounded by the conversion (a sum of 0.4 read as
    // 0 and 2.5 as 2, for a double or float; a decimal cannot be kept at all), so it is refused by name before anything
    // is executed. AggregateSum, read from AggregatesAsync, keeps the type of the column.
    internal static class LongSum
    {
        internal static void Require(Expression column)
        {
            var type = TypeOf(column);
            if (type == typeof(decimal)) throw Refusal("a decimal column");
            if (type == typeof(double) || type == typeof(float))
                throw Refusal("a double or float column (a sum of 0.4 would read as 0 and a sum of 2.5 as 2)");
        }

        private static Type TypeOf(Expression column)
        {
            while (column is UnaryExpression { NodeType: ExpressionType.Convert } convert) column = convert.Operand;
            return Nullable.GetUnderlyingType(column.Type) ?? column.Type;
        }

        private static InvalidOperationException Refusal(string what) =>
            new($"SumAsync returns a long and would round {what}; "
                + "add it with AggregateSum and read it from AggregatesAsync.");
    }
