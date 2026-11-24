#nullable enable
using System;
using System.Collections.Generic;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    internal sealed class SetClauses
    {
        private readonly Func<string, string> _quote;
        private readonly List<Node> _sets = new List<Node>();
        private readonly List<string> _setColumns = new List<string>();
        private bool _mirrored;

        internal SetClauses(Func<string, string> quote)
        {
            _quote = quote;
        }

        internal int Count => _sets.Count;

        internal NodeList Assignments => NodeList.Of(_sets);

        internal void Add(string column, Node value)
        {
            if (_mirrored)
                throw new InvalidOperationException(
                    "this statement already merges into a JSON document, and a Set() after it would "
                    + "assign a column the merge has already been built beside. Build every other "
                    + "assignment first and merge last.");
            _sets.Add(new Assignment(new ColumnRef(column), value));
            _setColumns.Add(column);
        }

        internal void AddMirrored(string column, Node value)
        {
            Add(column, value);
            _mirrored = true;
        }

        internal void RequireNotMirrored()
        {
            if (_mirrored)
                throw new InvalidOperationException(
                    "this statement already merges into a JSON document; a second merge would be a "
                    + "second assignment to the same column and PostgreSQL refuses that with 42701.");
        }

        internal void RequireUnassigned(string column)
        {
            if (_setColumns.Contains(column))
                throw new InvalidOperationException(
                    $"this statement already assigns {_quote(column)}, so merging into it would be a second "
                    + "assignment to the same column -- PostgreSQL refuses that with 42701. A merge "
                    + "beside a whole-document assignment is also two answers to the question the "
                    + "merge exists to answer: what did THIS request change.");
        }
    }
