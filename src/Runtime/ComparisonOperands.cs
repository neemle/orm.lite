#nullable enable
using System;
using System.Linq.Expressions;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // A comparison is translated against a column of the entity being queried. When neither operand is one, there
    // is nothing for the database to compare, and the translator would otherwise take a member of a captured object
    // for a column named after it, or fail on a cast. The same holds for any selector the translator turns into a
    // column (an ordering, an aggregate, a SET target). It is refused by name before anything is rendered or bound.
    internal static class ComparisonOperands
    {
        internal static void RequireColumnSide(Expression left, Expression right)
        {
            if (PredicateTranslator.IsColumn(left) || PredicateTranslator.IsColumn(right)) return;
            throw new NotSupportedException(
                $"Cannot compare {left} with {right}: neither side is a column of the entity being queried, "
                + "so the database has nothing to compare. A member of a captured object, arithmetic over a "
                + "column and a comparison of two values are not columns. Nothing was executed. "
                + "Compare a column with a value, or work the value out first and pass the result.");
        }

        // `<`, `<=`, `>` and `>=` against NULL select no row whatever the column holds, so a null value (a null
        // variable, or a null literal) there is a mistake that used to render as a predicate that never matches.
        // Equality and inequality against null are IS NULL and IS NOT NULL and stay.
        internal static void RequireNonNullRelational(CompareOp op, object? value, MemberExpression column)
        {
            if (value is not null || op is CompareOp.Equal or CompareOp.NotEqual) return;
            throw new NotSupportedException(
                $"Cannot compare the column {column.Member.Name} with a null value using a relational operator "
                + "(<, <=, > or >=): the comparison is never true, so it would select no row at all. "
                + "Nothing was executed. Test the column with == null or != null, or work the value out first "
                + "and pass one that is not null.");
        }

        internal static void RequireColumn(Expression selector)
        {
            if (PredicateTranslator.IsColumn(selector)) return;
            throw new NotSupportedException(
                $"Cannot use {selector} as a column: it is not a column of the entity being queried. A member of a "
                + "captured object, a nested member and a computed value are not columns. Nothing was executed. "
                + "Name a column of the row, or work the value out first and pass the result.");
        }
    }
