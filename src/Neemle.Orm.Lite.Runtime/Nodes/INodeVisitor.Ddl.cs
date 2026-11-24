namespace Neemle.Orm.Lite.Nodes;

public partial interface INodeVisitor
{
    void Visit(TypeRef node);
    void Visit(RawSql node);
    void Visit(BareLit node);
    void Visit(NamedParam node);
    void Visit(ColumnDef node);
    void Visit(TablePrimaryKey node);
    void Visit(ForeignKeyDef node);
    void Visit(CreateTable node);
    void Visit(DropTable node);
    void Visit(AddColumn node);
    void Visit(DropColumn node);
    void Visit(RenameColumn node);
    void Visit(CreateIndex node);
    void Visit(DropIndex node);
    void Visit(CreateView node);
    void Visit(DropView node);
    void Visit(AddForeignKey node);
    void Visit(DropForeignKey node);
    void Visit(DataRow node);
    void Visit(InsertData node);
    void Visit(DeleteData node);
    void Visit(ConvertForeignKeyAction node);
    void Visit(ConvertToLiveOnlyIndex node);
    void Visit(SoftDeleteColumn node);
    void Visit(HistoryTableNode node);
    void Visit(RowHistoryFunction node);
    void Visit(RowHistoryInstall node);
    void Visit(Script node);
    void Visit(SessionLock node);
    void Visit(ReconcileIndex node);
    void Visit(ReconcileIndexes node);
}
