#nullable enable
using System;
using System.Linq.Expressions;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    // An enum column holds the member NAME as text, so a database that is asked to rank two of them ranks the
    // names alphabetically and not the values. Rather than answer with an order the enum does not have, a
    // relational comparison (<, <=, >, >=), an ordering, a Min or a Max and an order by the keys of a grouping over
    // an enum column are refused by name before anything is rendered or executed. Equality, inequality and IN stay:
    // they do not depend on any order.
    internal static class EnumOrdering
    {
        private const string Stored =
            "enums are stored as their member NAME, so the database would rank the names as text, "
            + "alphabetically and not by value, ";

        internal static void RefuseRelational(Expression left, Expression right, CompareOp op)
        {
            var column = EnumColumn(left) ?? EnumColumn(right);
            if (column is null) return;
            throw new NotSupportedException(
                $"Cannot compare the enum column {Describe(column)} with {Symbol(op)}: {Stored}"
                + "and the range would silently select the wrong rows. Nothing was executed. "
                + "Compare with == or != (or Contains) against the members you mean, or read the rows and "
                + "compare the enum values in memory.");
        }

        internal static void RefuseOrder(Expression selector)
        {
            var column = EnumColumn(selector);
            if (column is null) return;
            throw new NotSupportedException(
                $"Cannot order by the enum column {Describe(column)}: {Stored}"
                + "and the rows would silently come back in an order the enum does not have. "
                + "Nothing was executed. Order by a column that holds the order you mean (an integer rank, "
                + "for example), or read the rows and sort the enum values in memory.");
        }

        internal static void RefuseExtreme(Expression selector, string function)
        {
            var column = EnumColumn(selector);
            if (column is null) return;
            throw new NotSupportedException(
                $"Cannot take the {function} of the enum column {Describe(column)}: {Stored}"
                + "and the answer would silently be the wrong member. Nothing was executed. "
                + "Take it over a column that holds the order you mean (an integer rank, for example), "
                + "or read the rows and take it over the enum values in memory.");
        }

        internal static string? KeyName(Expression key) => EnumColumn(key) is { } column ? Describe(column) : null;

        internal static void RefuseKeyOrder(string? enumKey)
        {
            if (enumKey is null) return;
            throw new NotSupportedException(
                $"Cannot order the groups by the enum key {enumKey}: {Stored}"
                + "and the groups would silently come back in an order the enum does not have. "
                + "Nothing was executed. Order by an aggregate or by a column that holds the order you mean "
                + "(an integer rank, for example), or read the groups and sort the enum values in memory.");
        }

        // The column an expression names once every conversion around it is peeled away, when that column
        // is an enum or a nullable enum: `n.Status`, `(int)n.Status` and `(object)n.Status` all name it.
        private static MemberExpression? EnumColumn(Expression e)
        {
            while (e is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u)
                e = u.Operand;
            return e is MemberExpression { Expression: ParameterExpression } m
                && (Nullable.GetUnderlyingType(m.Type) ?? m.Type).IsEnum
                ? m
                : null;
        }

        private static string Describe(MemberExpression column)
        {
            var enumType = Nullable.GetUnderlyingType(column.Type) ?? column.Type;
            var nullable = enumType != column.Type ? "?" : "";
            return column.Member.Name + " (" + enumType.Name + nullable + ")";
        }

        private static string Symbol(CompareOp op) => op switch
        {
            CompareOp.Less => "<",
            CompareOp.LessOrEqual => "<=",
            CompareOp.Greater => ">",
            CompareOp.GreaterOrEqual => ">=",
            _ => op.ToString(),
        };
    }
