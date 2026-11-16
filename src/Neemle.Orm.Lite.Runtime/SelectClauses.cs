#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Neemle.Orm.Lite;

    internal sealed class SelectClauses
    {
        private readonly ISqlDialect D;
        private readonly string _table;
        private readonly List<string> _orders = new List<string>();
        private bool _rowLock;
        private bool _rowLockKeyPreserving;

        internal SelectClauses(ISqlDialect dialect, string table)
        {
            D = dialect;
            _table = table;
        }

        internal int? Limit { get; set; }

        internal int? Offset { get; set; }

        internal void ReplaceOrder(string term)
        {
            _orders.Clear();
            _orders.Add(term);
        }

        internal void AddOrder(string term) => _orders.Add(term);

        internal void LockRows() => _rowLock = true;

        internal void LockRowsKeyPreserving()
        {
            _rowLock = true;
            _rowLockKeyPreserving = true;
        }

        internal string Render(string columns, string whereText)
        {
            var sb = new StringBuilder("SELECT ").Append(columns)
                .Append(" FROM ").Append(D.Quote(_table)).Append(whereText);
            if (_orders.Count > 0) sb.Append(" ORDER BY ").Append(string.Join(", ", _orders));
            sb.Append(D.Page(Limit, Offset));
            if (_rowLock) sb.Append(_rowLockKeyPreserving ? D.KeyPreservingRowLockClause : D.RowLockClause);
            return sb.ToString();
        }
    }
