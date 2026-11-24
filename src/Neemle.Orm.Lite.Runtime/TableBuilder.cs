#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    public sealed class TableBuilder
    {
        private readonly List<Node> _cols = new List<Node>();
        private readonly List<Node> _constraints = new List<Node>();
        private readonly ISqlProvider _provider;

        public TableBuilder(ISqlProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            _provider = provider;
        }

        public IReadOnlyList<Node> Columns => _cols;

        public IReadOnlyList<Node> Constraints => _constraints;

        public TableBuilder Column(string name, ColumnType type, bool nullable = true, bool primaryKey = false, Node? defaultValue = null) =>
            Add(name, new TypeRef(type), nullable, primaryKey, defaultValue);

        public TableBuilder DecimalColumn(string name, int precision, int scale, bool nullable = true, bool primaryKey = false, Node? defaultValue = null) =>
            Add(name, new TypeRef(ColumnType.Decimal, precision, scale), nullable, primaryKey, defaultValue);

        internal TableBuilder AddConstraint(Node constraint)
        {
            _constraints.Add(constraint);
            return this;
        }

        public TableBuilder JsonbColumn(string name, bool nullable = true) =>
            Column(name, ColumnType.Json, nullable, false, nullable ? null : Defaults.EmptyDocument);

        public string Render()
        {
            return string.Join(", ", _cols.Select(c => _provider.CreateSerializer().Fragment(c)));
        }

        private TableBuilder Add(string name, Node type, bool nullable, bool primaryKey, Node? defaultValue)
        {
            _cols.Add(new ColumnDef(name, type, nullable, primaryKey, defaultValue));
            return this;
        }
    }
