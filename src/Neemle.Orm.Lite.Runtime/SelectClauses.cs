#nullable enable
using System;
using System.Collections.Generic;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    internal sealed class SelectClauses
    {
        private readonly string _table;
        private readonly List<Node> _orders = new List<Node>();
        private bool _rowLock;
        private bool _rowLockKeyPreserving;

        internal SelectClauses(string table)
        {
            _table = table;
        }

        private int? _limit;
        private int? _offset;

        internal int? Limit { get => _limit; set => _limit = value is int n ? NonNegative(n, "Take") : null; }

        internal int? Offset { get => _offset; set => _offset = value is int n ? NonNegative(n, "Skip") : null; }

        // ORML-62: a negative count is refused where it is written. LIMIT -1 is an error on PostgreSQL and means
        // "no limit" on SQLite, so one text would read two ways.
        internal static int NonNegative(int count, string method) => count >= 0
            ? count
            : throw new ArgumentOutOfRangeException(
                "n", count, method + " takes a count of zero or more; a negative count is refused on every provider.");

        internal void ReplaceOrder(Node term)
        {
            _orders.Clear();
            _orders.Add(term);
        }

        internal void AddOrder(Node term) => _orders.Add(term);

        internal void LockRows() => _rowLock = true;

        internal void LockRowsKeyPreserving()
        {
            _rowLock = true;
            _rowLockKeyPreserving = true;
        }

        internal QueryClauses Present =>
            (Limit is null ? QueryClauses.None : QueryClauses.Take)
            | (Offset is null ? QueryClauses.None : QueryClauses.Skip)
            | (_orders.Count == 0 ? QueryClauses.None : QueryClauses.OrderBy)
            | (_rowLock ? QueryClauses.RowLock : QueryClauses.None);

        internal Select Build(IEnumerable<Node> columns, Where? where) =>
            Compose(columns, where, Limit, lockRows: true);

        internal Select BuildScalar(IEnumerable<Node> columns, Where? where)
        {
            ClauseGuard.Require(this, "ScalarAsync", ClauseRule.Scalar);
            return BuildFirst(columns, where);
        }

        internal Select BuildFirst(IEnumerable<Node> columns, Where? where, bool lockRows = false) =>
            Compose(columns, where, Limit is int limit ? Math.Min(1, limit) : 1, lockRows);

        private Select Compose(IEnumerable<Node> columns, Where? where, int? limit, bool lockRows) =>
            new Select(
                NodeList.Of(columns),
                new TableRef(_table),
                where,
                _orders.Count > 0 ? new OrderBy(NodeList.Of(_orders)) : null,
                limit is int rows ? new Nodes.Limit(rows) : null,
                Offset is int offset ? new Nodes.Offset(offset) : null,
                lockRows && _rowLock
                    ? new RowLock(_rowLockKeyPreserving ? RowLockStrength.KeyPreserving : RowLockStrength.Update)
                    : null);
    }
