#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.OrmLite;

    [Flags]
    internal enum QueryClauses
    {
        None = 0,
        Take = 1,
        Skip = 2,
        OrderBy = 4,
        RowLock = 8,
    }

    internal enum ClauseRule
    {
        Write,
        Aggregate,
        Grouping,
        Scalar,
    }

    internal static class ClauseGuard
    {
        private const QueryClauses Paging = QueryClauses.Take | QueryClauses.Skip | QueryClauses.OrderBy;

        private static readonly (QueryClauses Clause, string Name)[] Order =
        [
            (QueryClauses.Take, "Take"),
            (QueryClauses.Skip, "Skip"),
            (QueryClauses.OrderBy, "OrderBy"),
            (QueryClauses.RowLock, "ForUpdate or ForNoKeyUpdate"),
        ];

        internal static void Require(SelectClauses clauses, string operation, ClauseRule rule)
        {
            var refused = clauses.Present & Refused(rule);
            if (refused == QueryClauses.None) return;
            throw new InvalidOperationException(
                $"{operation} cannot honour {Names(refused)}: {Reasons(rule, refused)} {Outcome(rule)} "
                + Advices(rule, refused));
        }

        private static QueryClauses Refused(ClauseRule rule) => rule switch
        {
            ClauseRule.Aggregate => QueryClauses.Take | QueryClauses.Skip | QueryClauses.RowLock,
            ClauseRule.Scalar => QueryClauses.RowLock,
            _ => Paging | QueryClauses.RowLock,
        };

        private static string Names(QueryClauses refused) =>
            string.Join(", ", Order.Where(entry => refused.HasFlag(entry.Clause)).Select(entry => entry.Name));

        private static string Reasons(ClauseRule rule, QueryClauses refused) =>
            Joined(refused, Reason(rule), LockReason(rule));

        private static string Advices(ClauseRule rule, QueryClauses refused) =>
            Joined(refused, Advice(rule, refused), LockAdvice(rule));

        private static string Joined(QueryClauses refused, string paging, string rowLock)
        {
            var parts = new List<string>();
            if ((refused & Paging) != QueryClauses.None) parts.Add(paging);
            if (refused.HasFlag(QueryClauses.RowLock)) parts.Add(rowLock);
            return string.Join(" ", parts);
        }

        private static string Reason(ClauseRule rule) => rule switch
        {
            ClauseRule.Write =>
                "a write affects every row that matches Where, so a query that pages or orders its rows is refused.",
            ClauseRule.Aggregate =>
                "an aggregate covers every row that matches Where, so a query that pages its rows would be "
                + "answered over a different set of rows.",
            _ => "a grouping covers every row that matches Where, and a page or an order set on the query is "
                + "dropped by it.",
        };

        private static string LockReason(ClauseRule rule) => rule switch
        {
            ClauseRule.Write =>
                "a write locks the rows it changes by itself, so the row lock set on the query would be dropped.",
            ClauseRule.Aggregate =>
                "an aggregate returns no row to lock, so the row lock set on the query would be dropped.",
            ClauseRule.Scalar =>
                "a scalar read takes no row lock, so the row lock set on the query would be dropped.",
            _ => "a grouping returns no row to lock, so the row lock set on the query would be dropped.",
        };

        private static string Outcome(ClauseRule rule) => rule switch
        {
            ClauseRule.Write => "Nothing was executed and no row was changed.",
            _ => "Nothing was executed.",
        };

        // The WhereIn hint names only the paging clauses that are set; a clause that is not set is never named.
        private static string Advice(ClauseRule rule, QueryClauses refused) => rule switch
        {
            ClauseRule.Write =>
                "Select the keys of the rows you mean first, with the same filters through ProjectAsync, "
                + "and write by those keys, or filter the write with WhereIn over a subquery that carries the "
                + string.Join(" and ", PagingNames(refused)) + ".",
            ClauseRule.Aggregate => "Remove the paging; an ordering alone is allowed and changes nothing.",
            _ => "Put the paging and the ordering on the grouping itself.",
        };

        private static IEnumerable<string> PagingNames(QueryClauses refused) =>
            Order.Where(entry => (entry.Clause & Paging) != QueryClauses.None && refused.HasFlag(entry.Clause))
                .Select(entry => entry.Name);

        private static string LockAdvice(ClauseRule rule) => rule switch
        {
            ClauseRule.Write =>
                "Remove the lock, or lock the rows first by reading them in a unit of work and write by their keys.",
            ClauseRule.Scalar =>
                "Remove the lock, or read the row with FirstOrDefaultAsync or ToListAsync, which take it.",
            _ => "Remove the lock, or read the rows to lock with FirstOrDefaultAsync or ToListAsync, which take it.",
        };
    }
