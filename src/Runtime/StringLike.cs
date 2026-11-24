#nullable enable
using System;
using System.Linq.Expressions;

namespace Neemle.OrmLite;

    // The pieces of a StartsWith, EndsWith or Contains over a column that the translator turns into LIKE: the pattern,
    // and whether the call asked to ignore case. The overloads that do are StartsWith and EndsWith with a
    // StringComparison, with a bool and a culture (ignoreCase is the second argument), and Contains with a
    // StringComparison. A null value is refused because the C# call throws for it and LIKE '%%' would match every row.
    internal static class StringLike
    {
        internal static string Pattern(string method, object? value)
        {
            var text = value as string ?? throw new ArgumentNullException(
                "value",
                $"{method} over a column needs a string to look for, and C# throws for null where LIKE '%%' "
                + "would match every row that is not NULL. Pass a string, or test the column with == null.");
            return method switch
            {
                "StartsWith" => LikePattern.StartsWith(text),
                "EndsWith" => LikePattern.EndsWith(text),
                _ => LikePattern.Contains(text),
            };
        }

        internal static bool IgnoresCase(MethodCallExpression call, Func<Expression, object?> evaluate) =>
            call.Arguments.Count switch
            {
                2 => evaluate(call.Arguments[1]) is StringComparison comparison && IsIgnoreCase(comparison),
                3 => evaluate(call.Arguments[1]) is true,
                _ => false,
            };

        private static bool IsIgnoreCase(StringComparison comparison) =>
            comparison is StringComparison.OrdinalIgnoreCase
                or StringComparison.InvariantCultureIgnoreCase
                or StringComparison.CurrentCultureIgnoreCase;
    }
