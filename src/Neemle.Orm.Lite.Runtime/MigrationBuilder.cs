#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.Orm.Lite;

    public sealed class MigrationBuilder
    {
        private readonly List<string> _ops = new List<string>();
        private readonly IDdlDialect _dialect;
        public IReadOnlyList<string> Operations => _ops;

        public MigrationBuilder() : this(new PgDdlDialect()) { }
        public MigrationBuilder(IDdlDialect dialect) { _dialect = dialect; }

        public MigrationBuilder Sql(string sql) { _ops.Add(sql); return this; }

        public MigrationBuilder CreateTable(string name, Action<TableBuilder> build)
        {
            var t = new TableBuilder(_dialect);
            build(t);
            _ops.Add("CREATE TABLE IF NOT EXISTS " + _dialect.Quote(name) + " (" + t.Render() + ")");
            return this;
        }

        public MigrationBuilder DropTable(string name)
        { _ops.Add("DROP TABLE IF EXISTS " + _dialect.Quote(name)); return this; }

        public MigrationBuilder AddColumn(string table, string name, string type, bool nullable = true, string? defaultSql = null)
        {
            _ops.Add("ALTER TABLE " + _dialect.Quote(table) + " ADD COLUMN IF NOT EXISTS " + _dialect.Quote(name) + " " + type
                + (nullable ? "" : " NOT NULL") + (defaultSql != null ? " DEFAULT " + defaultSql : ""));
            return this;
        }

        public MigrationBuilder AddJsonbColumn(string table, string name, bool nullable = true) =>
            AddColumn(table, name, Jsonb.ColumnType, nullable, nullable ? null : Jsonb.EmptyDocumentDefaultSql);

        public MigrationBuilder DropColumn(string table, string name)
        { _ops.Add("ALTER TABLE " + _dialect.Quote(table) + " DROP COLUMN IF EXISTS " + _dialect.Quote(name)); return this; }

        public MigrationBuilder RenameColumn(string table, string from, string to)
        { _ops.Add("ALTER TABLE " + _dialect.Quote(table) + " RENAME COLUMN " + _dialect.Quote(from) + " TO " + _dialect.Quote(to)); return this; }

        public MigrationBuilder CreateIndex(string name, string table, params string[] columns)
        {
            _ops.Add("CREATE INDEX IF NOT EXISTS " + _dialect.Quote(name) + " ON " + _dialect.Quote(table) + " ("
                + string.Join(", ", columns.Select(_dialect.Quote)) + ")");
            return this;
        }

        public MigrationBuilder CreateUniqueIndex(string name, string table, params string[] columns)
        {
            _ops.Add("CREATE UNIQUE INDEX IF NOT EXISTS " + _dialect.Quote(name) + " ON "
                + _dialect.Quote(table) + " ("
                + string.Join(", ", columns.Select(_dialect.Quote)) + ")");
            return this;
        }

        public MigrationBuilder DropIndex(string name)
        { _ops.Add("DROP INDEX IF EXISTS " + _dialect.Quote(name)); return this; }

        public MigrationBuilder AddForeignKey(string name, string table, string column, string refTable, string refColumn)
        {
            _ops.Add("ALTER TABLE " + _dialect.Quote(table) + " ADD CONSTRAINT " + _dialect.Quote(name) + " FOREIGN KEY (" + _dialect.Quote(column)
                + ") REFERENCES " + _dialect.Quote(refTable) + " (" + _dialect.Quote(refColumn) + ")");
            return this;
        }

        public MigrationBuilder InsertData(string table, string[] columns, params object?[][] rows)
        {
            var values = string.Join(", ", rows.Select(r => "(" + string.Join(", ", r.Select(_dialect.Literal)) + ")"));
            _ops.Add("INSERT INTO " + _dialect.Quote(table) + " (" + string.Join(", ", columns.Select(_dialect.Quote))
                + ") VALUES " + values);
            return this;
        }
    }
