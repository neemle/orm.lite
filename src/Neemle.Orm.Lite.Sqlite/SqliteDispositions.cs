using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

public enum Disposition
{
    Statement,
    Planned,
    Refused,
}

public static class SqliteDispositions
{
    public static Disposition Of(Node node) => node switch
    {
        TypeRef or RawSql or BareLit or NamedParam or ColumnDef or TablePrimaryKey or ForeignKeyDef or CreateTable or DropTable
            or RenameColumn or CreateIndex or DropIndex or DropView or DataRow or InsertData or DeleteData or Script => Disposition.Statement,
        AddColumn or DropColumn or AddForeignKey or DropForeignKey or ConvertForeignKeyAction or CreateView
            or ConvertToLiveOnlyIndex or SoftDeleteColumn or ReconcileIndex or ReconcileIndexes or RowHistoryFunction or RowHistoryInstall => Disposition.Planned,
        HistoryTableNode or SessionLock => Disposition.Refused,
        SqliteNode => Disposition.Statement,
        _ => throw new NotSupportedException($"{node.Name} has no recorded disposition on provider 'sqlite'."),
    };

    internal static bool NativeAddable(ColumnDef column)
    {
        if (column.PrimaryKey) return false;
        return column.Default switch
        {
            null => column.Nullable,
            NullLit => column.Nullable,
            StringLit or IntLit or BoolLit or BareLit => true,
            Cast { Type: CastType.Json, Operand: StringLit } => true,
            _ => false,
        };
    }

    internal static bool MayRebuild(Node operation) => operation switch
    {
        AddForeignKey or DropForeignKey or ConvertForeignKeyAction => true,
        AddColumn n => !NativeAddable(n.Column),
        Script n => n.Statements.Any(MayRebuild),
        _ => false,
    };
}

internal static class SqliteLowering
{
    internal static Node Lower(Node tree) => tree switch
    {
        AddColumn { IfNotExists: false } n when SqliteDispositions.NativeAddable(n.Column) => tree,
        DropColumn { IfExists: false } => tree,
        AddColumn or DropColumn or AddForeignKey or DropForeignKey or ConvertForeignKeyAction or CreateView or ConvertToLiveOnlyIndex or SoftDeleteColumn or ReconcileIndex or ReconcileIndexes or RowHistoryFunction or RowHistoryInstall =>
            throw new NotSupportedException(
                $"{tree.Name} is planned against the live catalog by provider 'sqlite' and has no static statement; run it through MigrationRunner."),
        _ => tree,
    };
}
