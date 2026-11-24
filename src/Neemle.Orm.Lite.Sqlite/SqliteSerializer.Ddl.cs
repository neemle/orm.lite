using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Sqlite;

public sealed partial class SqliteSerializer : ISqliteVisitor
{
    public override void Visit(TypeRef node)
    {
        switch (node.Kind)
        {
            case ColumnType.Uuid:
            case ColumnType.Text:
            case ColumnType.Timestamp:
            case ColumnType.Date:
            case ColumnType.Json:
                W.Atom("text");
                break;
            case ColumnType.Int32:
            case ColumnType.Int64:
                W.Atom("integer");
                break;
            case ColumnType.Bool:
                W.Atom("boolean");
                break;
            case ColumnType.Double:
                W.Atom("real");
                break;
            case ColumnType.Decimal when SqliteDecimal.Accepts(node):
                W.Atom("text");
                break;
            case ColumnType.Decimal:
                throw SqliteDecimal.Refusal(node);
            default:
                throw new NotSupportedException($"{nameof(SqliteSerializer)} has no spelling for the column type {node.Kind}.");
        }
    }

    public override void Visit(ColumnDef node)
    {
        W.Ident(node.Column);
        Write(node.Type);
        if (node.Type is TypeRef { Kind: ColumnType.Json })
            W.Atom(SqliteJsonDataLiterals.CheckKeyword).Open().Func(SqliteJsonDataLiterals.ValidityFunction).Ident(node.Column).Close().Close();
        if (node.Type is TypeRef { Kind: ColumnType.Decimal } decimalType)
        {
            W.Atom(SqliteDecimal.CollateKeyword).Atom(SqliteDecimal.Collation);
            W.Atom(SqliteJsonDataLiterals.CheckKeyword).Open().Func(SqliteDecimal.FitsFunction).Ident(node.Column)
                .Comma().Int(decimalType.Precision).Comma().Int(decimalType.Scale).Close().Close();
        }
        if (!node.Nullable || node.PrimaryKey) W.Kw(Kw.Not).Kw(Kw.Null);
        if (node.PrimaryKey) W.Ddl(DdlKw.Primary).Kw(Kw.Key);
        if (node.Default is not null)
        {
            W.Ddl(DdlKw.Default);
            Write(node.Default is Cast { Type: CastType.Json, Operand: StringLit document } ? document : node.Default);
        }
    }

    public override void Visit(AddColumn node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Add).Ddl(DdlKw.Column);
        Write(node.Column);
    }

    public override void Visit(DropColumn node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Column).Ident(node.Column);
    }

    public override void Visit(AddForeignKey node) => throw Planned(node);

    public override void Visit(DropForeignKey node) => throw Planned(node);

    private static NotSupportedException Planned(Node node) =>
        new NotSupportedException($"{node.Name} is planned against the live catalog by provider 'sqlite' and has no statement of its own; run it through MigrationRunner.");

    public void Visit(SqlitePragma node)
    {
        W.Atom("PRAGMA").Atom(node.Pragma);
        if (node.Argument is not null) W.CallOpen().Ident(node.Argument).Close();
        if (node.Value is not null) W.Atom("=").Atom(node.Value);
    }

    public void Visit(SqliteStored node) => W.Atom(node.Text);

    public void Visit(SqliteCreateTableFrom node)
    {
        W.Ddl(DdlKw.Create).Ddl(DdlKw.Table).Ident(node.Table).Open();
        WriteSeparated(node.Definitions);
        W.Close();
        if (node.Tail.Length > 0) W.Atom(node.Tail);
    }

    public void Visit(SqliteCopyRows node)
    {
        W.Ddl(DdlKw.Insert).Ddl(DdlKw.Into).Ident(node.To);
        WriteColumnList(node.Columns);
        W.Kw(Kw.Select);
        WriteSeparated(node.Columns);
        W.Kw(Kw.From).Ident(node.From);
    }

    public void Visit(SqliteRenameTable node)
    {
        WriteAlterTable(node.From);
        W.Ddl(DdlKw.Rename).Ddl(DdlKw.To).Ident(node.To);
    }

    public void Visit(SqliteDropObject node)
    {
        W.Ddl(DdlKw.Drop).Atom(node.Kind);
        WriteIfExists();
        W.Ident(node.Object);
    }

    public void Visit(SqliteDuplicateKeys node)
    {
        W.Kw(Kw.Select).Int(1).Kw(Kw.From).Ident(node.Table).Kw(Kw.Where);
        WriteConjunction(node.KeyColumns.Select(c => (Node)new IsNotNull(c)));
        W.Atom("GROUP").Kw(Kw.By);
        WriteSeparated(node.KeyTerms);
        W.Atom("HAVING").Func("count").Atom("*").Close().Atom(">").Int(1).Kw(Kw.Limit).Int(1);
    }
}
