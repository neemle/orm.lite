#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.Orm.Lite;

    public sealed class TableBuilder
    {
        private readonly List<string> _cols = new List<string>();
        private readonly IDdlDialect _dialect;

        public TableBuilder() : this(new PgDdlDialect()) { }
        public TableBuilder(IDdlDialect dialect) { _dialect = dialect; }

        public TableBuilder Column(string name, string type, bool nullable = true, bool primaryKey = false, string? defaultSql = null)
        {
            _cols.Add(_dialect.Quote(name) + " " + type + (nullable ? "" : " NOT NULL") + (primaryKey ? " PRIMARY KEY" : "")
                + (defaultSql != null ? " DEFAULT " + defaultSql : ""));
            return this;
        }

        public TableBuilder JsonbColumn(string name, bool nullable = true) =>
            Column(name, Jsonb.ColumnType, nullable, false, nullable ? null : Jsonb.EmptyDocumentDefaultSql);

        public string Render() => string.Join(", ", _cols);
    }
