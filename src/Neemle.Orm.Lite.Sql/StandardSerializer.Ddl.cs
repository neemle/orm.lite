using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sql;

public abstract partial class StandardSerializer
{
    private readonly Dictionary<string, int> _namedParameters = new(StringComparer.Ordinal);

    protected void WriteAlterTable(string table) => W.Ddl(DdlKw.Alter).Ddl(DdlKw.Table).Ident(table);

    protected void WriteIfExists() => W.Ddl(DdlKw.If).Kw(Kw.Exists);

    protected void WriteIfNotExists() => W.Ddl(DdlKw.If).Kw(Kw.Not).Kw(Kw.Exists);

    protected virtual void WriteReferentialAction(ReferentialAction action)
    {
        switch (action)
        {
            case ReferentialAction.NoAction:
                W.Kw(Kw.No).Ddl(DdlKw.Action);
                break;
            case ReferentialAction.Restrict:
                W.Ddl(DdlKw.Restrict);
                break;
            case ReferentialAction.Cascade:
                W.Ddl(DdlKw.Cascade);
                break;
            case ReferentialAction.SetNull:
                W.Ddl(DdlKw.Set).Kw(Kw.Null);
                break;
            default:
                throw new NotSupportedException($"{GetType().Name} has no spelling for the referential action {action}.");
        }
    }

    protected void WriteColumnList(IEnumerable<Node> columns)
    {
        W.Open();
        WriteSeparated(columns);
        W.Close();
    }

    protected void WriteAddConstraint(AddForeignKey node)
    {
        W.Ddl(DdlKw.Add);
        WriteForeignKeyClause(node.Constraint, node.Columns, node.ReferencedTable, node.ReferencedColumns, node.OnDelete, node.OnUpdate);
    }

    protected void WriteForeignKeyClause(
        string constraint, NodeList columns, string referencedTable, NodeList referencedColumns, ReferentialAction? onDelete, ReferentialAction? onUpdate)
    {
        W.Ddl(DdlKw.Constraint).Ident(constraint).Ddl(DdlKw.Foreign).Kw(Kw.Key);
        WriteColumnList(columns);
        W.Ddl(DdlKw.References).Ident(referencedTable);
        WriteColumnList(referencedColumns);
        if (onUpdate is { } update)
        {
            W.Ddl(DdlKw.On).Kw(Kw.Update);
            WriteReferentialAction(update);
        }
        if (onDelete is { } delete)
        {
            W.Ddl(DdlKw.On).Ddl(DdlKw.Delete);
            WriteReferentialAction(delete);
        }
    }

    public virtual void Visit(TypeRef node)
    {
        switch (node.Kind)
        {
            case ColumnType.Text:
                W.Atom("text");
                break;
            case ColumnType.Int32:
                W.Atom("integer");
                break;
            case ColumnType.Int64:
                W.Atom("bigint");
                break;
            case ColumnType.Bool:
                W.Atom("boolean");
                break;
            case ColumnType.Double:
                W.Atom("double").Atom("precision");
                break;
            case ColumnType.Date:
                W.Atom("date");
                break;
            case ColumnType.Decimal when node.Precision > 0:
                W.Func("numeric").Int(node.Precision).Comma().Int(node.Scale).Close();
                break;
            case ColumnType.Decimal:
                W.Atom("numeric");
                break;
            default:
                throw new NotSupportedException($"{GetType().Name} has no spelling for the column type {node.Kind}.");
        }
    }

    public virtual void Visit(RawSql node) => W.Atom(node.Text);

    public virtual void Visit(BareLit node) => W.Atom(node.Text);

    public virtual void Visit(NamedParam node)
    {
        if (_inlineParameters)
        {
            Write(ParamLiterals.Of(node.Value));
            return;
        }
        if (!_namedParameters.TryGetValue(node.Parameter, out var ordinal))
        {
            ordinal = _parameters.Count == 0 ? 0 : _parameters.Max(p => p.Ordinal) + 1;
            _namedParameters[node.Parameter] = ordinal;
            _parameters.Add(new StatementParameter(ordinal, node.Parameter, node.Value));
        }
        W.Param(ParameterPrefix, node.Parameter);
    }

    public virtual void Visit(ColumnDef node)
    {
        W.Ident(node.Column);
        Write(node.Type);
        if (!node.Nullable) W.Kw(Kw.Not).Kw(Kw.Null);
        if (node.PrimaryKey) W.Ddl(DdlKw.Primary).Kw(Kw.Key);
        if (node.Default is not null)
        {
            W.Ddl(DdlKw.Default);
            Write(node.Default);
        }
    }

    public virtual void Visit(TablePrimaryKey node)
    {
        W.Ddl(DdlKw.Primary).Kw(Kw.Key);
        WriteColumnList(node.Columns);
    }

    public virtual void Visit(ForeignKeyDef node) =>
        WriteForeignKeyClause(node.Constraint, node.Columns, node.ReferencedTable, node.ReferencedColumns, node.OnDelete, node.OnUpdate);

    public virtual void Visit(CreateTable node)
    {
        W.Ddl(DdlKw.Create).Ddl(DdlKw.Table);
        WriteIfNotExists();
        W.Ident(node.Table);
        W.Open();
        WriteSeparated(node.Columns);
        foreach (var constraint in node.Constraints ?? NodeList.Empty)
        {
            W.Comma();
            Write(constraint);
        }
        W.Close();
    }

    public virtual void Visit(DropTable node)
    {
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Table);
        WriteIfExists();
        W.Ident(node.Table);
    }

    public virtual void Visit(AddColumn node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Add).Ddl(DdlKw.Column);
        if (node.IfNotExists) WriteIfNotExists();
        Write(node.Column);
    }

    public virtual void Visit(DropColumn node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Column);
        if (node.IfExists) WriteIfExists();
        W.Ident(node.Column);
    }

    public virtual void Visit(RenameColumn node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Rename).Ddl(DdlKw.Column).Ident(node.From).Ddl(DdlKw.To).Ident(node.To);
    }

    public virtual void Visit(CreateIndex node)
    {
        W.Ddl(DdlKw.Create);
        if (node.Unique) W.Ddl(DdlKw.Unique);
        W.Ddl(DdlKw.Index);
        WriteIfNotExists();
        W.Ident(node.Index).Ddl(DdlKw.On).Ident(node.Table);
        WriteColumnList(node.Terms);
        if (node.Where is not null) Write(node.Where);
    }

    public virtual void Visit(DropIndex node)
    {
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Index);
        if (node.IfExists) WriteIfExists();
        W.Ident(node.Index);
    }

    public virtual void Visit(CreateView node)
    {
        W.Ddl(DdlKw.Create);
        if (node.Replace) W.Kw(Kw.Or).Ddl(DdlKw.Replace);
        W.Ddl(DdlKw.View).Ident(node.View).Kw(Kw.As);
        _inlineParameters = true;
        try
        {
            Write(node.Query);
        }
        finally
        {
            _inlineParameters = false;
        }
    }

    public virtual void Visit(DropView node)
    {
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.View);
        if (node.IfExists) WriteIfExists();
        W.Ident(node.View);
    }

    public virtual void Visit(AddForeignKey node)
    {
        WriteAlterTable(node.Table);
        WriteAddConstraint(node);
    }

    public virtual void Visit(DropForeignKey node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Constraint).Ident(node.Constraint);
    }

    public virtual void Visit(DataRow node)
    {
        W.Open();
        WriteSeparated(node.Values);
        W.Close();
    }

    public virtual void Visit(InsertData node)
    {
        W.Ddl(DdlKw.Insert).Ddl(DdlKw.Into).Ident(node.Table);
        WriteColumnList(node.Columns);
        W.Ddl(DdlKw.Values);
        if (node.Rows.Count == 0) W.Blank();
        WriteSeparated(node.Rows);
    }

    public virtual void Visit(DeleteData node)
    {
        W.Ddl(DdlKw.Delete).Kw(Kw.From).Ident(node.Table).Kw(Kw.Where).Ident(node.Column).Kw(Kw.In).Open();
        WriteSeparated(node.Values);
        W.Close();
    }

    public virtual void Visit(SessionLock node) =>
        throw new NotSupportedException($"{GetType().Name} has no spelling for a session lock.");

    public virtual void Visit(ConvertForeignKeyAction node) => throw MustBeLowered(node);

    public virtual void Visit(ConvertToLiveOnlyIndex node) => throw MustBeLowered(node);

    public virtual void Visit(SoftDeleteColumn node) => throw MustBeLowered(node);

    public virtual void Visit(HistoryTableNode node) => throw MustBeLowered(node);

    public virtual void Visit(RowHistoryFunction node) => throw MustBeLowered(node);

    public virtual void Visit(RowHistoryInstall node) => throw MustBeLowered(node);

    public virtual void Visit(Script node) => throw MustBeLowered(node);

    public virtual void Visit(ReconcileIndex node) => throw MustBeLowered(node);

    public virtual void Visit(ReconcileIndexes node) => throw MustBeLowered(node);

    private NotSupportedException MustBeLowered(Node node) =>
        new NotSupportedException($"{node.Name} reaches {GetType().Name} only after the provider has lowered it; compile it with DdlCompiler.");
}
