#nullable enable
using System;
using System.Collections.Generic;

namespace Neemle.Orm.Lite;

    internal sealed class SetClauses
    {
        private readonly ISqlDialect D;
        private readonly List<string> _sets = new List<string>();
        private readonly List<string> _setColumns = new List<string>();
        private bool _mirrored;

        internal SetClauses(ISqlDialect dialect)
        {
            D = dialect;
        }

        internal int Count => _sets.Count;

        internal string Joined => string.Join(", ", _sets);

        internal void Add(string quotedColumn, string assignment)
        {
            if (_mirrored)
                throw new InvalidOperationException(
                    "this statement already merges into a JSON document, and a Set() after it would "
                    + "assign a column the merge has already been built beside. Build every other "
                    + "assignment first and merge last.");
            var prefix = quotedColumn + " = ";
            if (!assignment.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"{D.GetType().Name} built the assignment `{assignment}`, which does not begin with "
                    + $"`{prefix}`. This type records an assignment's TARGET so a later merge can "
                    + "refuse a second assignment to the same column; an assignment it cannot split is "
                    + "one it cannot attribute to a column, and it is refused rather than recorded "
                    + "against the wrong one.");
            _sets.Add(assignment);
            _setColumns.Add(quotedColumn);
        }

        internal void AddMirrored(string quotedColumn, string assignment)
        {
            Add(quotedColumn, assignment);
            _mirrored = true;
        }

        internal void RequireNotMirrored()
        {
            if (_mirrored)
                throw new InvalidOperationException(
                    "this statement already merges into a JSON document; a second merge would be a "
                    + "second assignment to the same column and PostgreSQL refuses that with 42701.");
        }

        internal void RequireUnassigned(string target)
        {
            if (_setColumns.Contains(target))
                throw new InvalidOperationException(
                    $"this statement already assigns {target}, so merging into it would be a second "
                    + "assignment to the same column -- PostgreSQL refuses that with 42701. A merge "
                    + "beside a whole-document assignment is also two answers to the question the "
                    + "merge exists to answer: what did THIS request change.");
        }
    }
