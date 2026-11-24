namespace Neemle.Orm.Lite.Nodes;

public partial interface INodeVisitor
{
    void Visit(HistoryChangedKeys node);
    void Visit(HistoryStateQuery node);
    void Visit(HistoryRevertUpdate node);
    void Visit(ExistingTablesQuery node);
    void Visit(ConstraintColumnsOf node);
    void Visit(HistorySoftDeleteLookup node);
    void Visit(SoftTrashBranch node);
    void Visit(SoftTrashUnion node);
}
