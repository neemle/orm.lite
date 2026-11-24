#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    public sealed class MigrationBuilder
    {
        private readonly List<Node> _ops = new List<Node>();
        private readonly Func<ISqlProvider> _provider;

        public MigrationBuilder() : this(new PgDdlDialect()) { }
        public MigrationBuilder(IDdlDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(dialect);
            _provider = () => dialect.Provider;
        }
        public MigrationBuilder(ISqlProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            _provider = () => provider;
        }

        private MigrationBuilder(Func<ISqlProvider> provider) => _provider = provider;

        internal MigrationBuilder Scratch() => new(_provider);

        public IReadOnlyList<Node> Nodes => _ops;

        public IReadOnlyList<string> Operations => DdlCompiler.Render(_ops, _provider());

        public IReadOnlyList<string> Render(ISqlProvider provider) => DdlCompiler.Render(_ops, provider);

        public MigrationBuilder Add(Node op)
        {
            ArgumentNullException.ThrowIfNull(op);
            _ops.Add(op);
            return this;
        }

        /// <summary>
        /// The one escape hatch for a statement the builder has no method for. The text is written for the provider
        /// named by <paramref name="providerId"/>, is refused when it is rendered for any other provider, and has no
        /// derived inverse, so a migration that holds it declares its own Down or is Irreversible. The caller owns
        /// the statement's dialect and safety; the library does not check it.
        /// </summary>
        public MigrationBuilder Raw(string providerId, string sql) => Add(new RawSql(providerId, sql));

        public MigrationBuilder CreateTable(string name, Action<TableBuilder> build)
        {
            var t = new TableBuilder();
            build(t);
            return Add(new CreateTable(name, NodeList.Of(t.Columns), t.Constraints.Count == 0 ? null : NodeList.Of(t.Constraints)));
        }

        public MigrationBuilder DropTable(string name) => Add(new DropTable(name));

        public MigrationBuilder AddColumn(string table, string name, ColumnType type, bool nullable = true, Node? defaultValue = null) =>
            Add(new AddColumn(table, new ColumnDef(name, new TypeRef(type), nullable, false, defaultValue)));

        public MigrationBuilder AddDecimalColumn(string table, string name, int precision, int scale, bool nullable = true, Node? defaultValue = null) =>
            Add(new AddColumn(table, new ColumnDef(name, new TypeRef(ColumnType.Decimal, precision, scale), nullable, false, defaultValue)));

        public MigrationBuilder AddJsonbColumn(string table, string name, bool nullable = true) =>
            AddColumn(table, name, ColumnType.Json, nullable, nullable ? null : Defaults.EmptyDocument);

        public MigrationBuilder DropColumn(string table, string name) => Add(new DropColumn(table, name));

        public MigrationBuilder RenameColumn(string table, string from, string to) => Add(new RenameColumn(table, from, to));

        public MigrationBuilder CreateIndex(string name, string table, params string[] columns) =>
            Add(new CreateIndex(name, table, DdlLiterals.Columns(columns)));

        public MigrationBuilder CreateUniqueIndex(string name, string table, params string[] columns) =>
            Add(new CreateIndex(name, table, DdlLiterals.Columns(columns), true));

        public MigrationBuilder CreateIndex(string name, string table, params IndexTerm[] terms) =>
            Add(new CreateIndex(name, table, NodeList.Of(terms.Select(t => t.ToNode()))));

        public MigrationBuilder CreateUniqueIndex(string name, string table, params IndexTerm[] terms) =>
            Add(new CreateIndex(name, table, NodeList.Of(terms.Select(t => t.ToNode())), true));

        public MigrationBuilder DropIndex(string name) => Add(new DropIndex(name));

        public MigrationBuilder AddForeignKey(string name, string table, string column, string refTable, string refColumn) =>
            Add(new AddForeignKey(name, table, DdlLiterals.Columns([column]), refTable, DdlLiterals.Columns([refColumn])));

        public MigrationBuilder DropForeignKey(string name, string table) => Add(new DropForeignKey(name, table));

        public MigrationBuilder InsertData(string table, string[] columns, params object?[][] rows)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Length == 0) throw new ArgumentException("InsertData needs at least one row.", nameof(rows));
            if (columns.Length == 0)
                throw new ArgumentException("InsertData needs at least one column.", nameof(columns));
            var data = rows.Select(r => (Node)new DataRow(NodeList.Of(r.Select(DdlLiterals.Of))));
            return Add(new InsertData(table, DdlLiterals.Columns(columns), NodeList.Of(data)));
        }

        public MigrationBuilder InsertData<T>(EntityDescriptor entity, Func<T, object?[]> values, params T[] rows)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(rows);
            return InsertData(entity.Table, entity.ColumnNames.ToArray(), rows.Select(r => values(r).Select(ParamLiterals.Normalise).ToArray()).ToArray());
        }

        public MigrationBuilder DeleteData(string table, string column, params object?[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length == 0) throw new ArgumentException("DeleteData needs at least one value to match.", nameof(values));
            if (values.Any(v => v is null))
                throw new ArgumentException(
                    "DeleteData cannot match a null value: an IN list never matches NULL, "
                    + "so the delete would silently skip it.",
                    nameof(values));
            return Add(new DeleteData(table, column, NodeList.Of(values.Select(DdlLiterals.Of))));
        }

        public Query<T> From<T>() => ViewDefinitions.Source<T>(_provider());

        public MigrationBuilder CreateView(ViewDefinition view) => Add(new CreateView(view.View, view.Query));

        public MigrationBuilder ReplaceView(ViewDefinition view) => Add(new CreateView(view.View, view.Query, true));

        public MigrationBuilder DropView(string name) => Add(new DropView(name));

        public MigrationBuilder CreateViews(params ViewDefinition[] views)
        {
            foreach (var view in ViewDefinitions.InDependencyOrder(views)) CreateView(view);
            return this;
        }

        public MigrationBuilder DropViews(params ViewDefinition[] views)
        {
            foreach (var view in ViewDefinitions.InDependencyOrder(views).Reverse()) DropView(view.View);
            return this;
        }
    }
