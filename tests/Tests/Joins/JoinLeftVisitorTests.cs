using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;
using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinLeftVisitorTests
{
    [Fact] public void Legacy_only_visitor_fails_closed_without_old_callback()
    {
        var visitor = new Legacy();
        var error = Assert.Throws<NotSupportedException>(() =>
        new LeftJoin(new("x"), new BoolLit(true)).Accept(visitor));
        Assert.Equal("Visitor does not support LeftJoin.", error.Message); Assert.Empty(visitor.Visits);
    }
    [Fact] public void Opt_in_traverses_actual_table_and_on()
    {
        var visitor = new OptIn(); new LeftJoin(new("x"), new BoolLit(true)).Accept(visitor);
        Assert.Equal(new[] { "LeftJoin", "TableRef", "BoolLit" }, visitor.Visits);
    }
    [Fact] public void Independent_children_walk_sees_parameter_and_capability_before_lowering()
    {
        var parameter = new Param(7, 9);
        var tree = new LeftJoin(new("x"), new And(parameter, new RowLock(RowLockStrength.KeyPreserving)));
        var nodes = Descendants(tree).ToArray(); Assert.Contains(parameter, nodes);
        Assert.Contains(Capability.KeyPreservingRowLock, StatementCompiler.RequiredBy(tree));
        var provider = new JoinCompilerProvider(SqliteProvider.Instance) { Declared = CapabilitySet.Nothing() };
        Assert.Throws<CapabilityNotSupportedException>(() =>
        StatementCompiler.Compile(tree, provider, StatementKind.Read));
        Assert.Equal(0, provider.Lowers);
    }
    [Fact] public void Legacy_serializer_refuses_compiler_before_any_io()
    {
        var f = new JoinCompilerFixture<JoinLeftVisitorTests>(); f.Provider.Serializer = new LegacySerializer();
        JoinCompilerFixture<JoinLeftVisitorTests>.Error(() => f.Build(), JoinError.UnsupportedOperation);
        Assert.Equal(0, f.Probe.Connections + f.Probe.Commands);
    }
    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.Children()) foreach (var node in Descendants(child)) yield return node;
    }
    private sealed class LegacySerializer : ISerializer
    {
        public SerializedStatement Serialize(Node node) { node.Accept(new Legacy()); return new("unreachable", []); }
        public string Fragment(Node node) { node.Accept(new Legacy()); return "unreachable"; }
    }
    private sealed class OptIn : Legacy, ILeftJoinNodeVisitor
    {
        public void Visit(LeftJoin node) => Record("LeftJoin", node);
    }
    private class Legacy : INodeVisitor
    {
        internal List<string> Visits { get; } = [];
        protected void Record(string name, Node node)
        {
            Visits.Add(name); foreach (var child in node.Children()) child.Accept(this);
        }
        public void Visit(Star node) => Record("Star", node);
        public void Visit(NullLit node) => Record("NullLit", node);
        public void Visit(ClockNow node) => Record("ClockNow", node);
        public void Visit(BoolLit node) => Record("BoolLit", node);
        public void Visit(IntLit node) => Record("IntLit", node);
        public void Visit(StringLit node) => Record("StringLit", node);
        public void Visit(AliasRef node) => Record("AliasRef", node);
        public void Visit(TableRef node) => Record("TableRef", node);
        public void Visit(Param node) => Record("Param", node);
        public void Visit(RowLock node) => Record("RowLock", node);
        public void Visit(ColumnRef node) => Record("ColumnRef", node);
        public void Visit(And node) => Record("And", node);
        public void Visit(Or node) => Record("Or", node);
        public void Visit(Not node) => Record("Not", node);
        public void Visit(Group node) => Record("Group", node);
        public void Visit(Compare node) => Record("Compare", node);
        public void Visit(IsNull node) => Record("IsNull", node);
        public void Visit(IsNotNull node) => Record("IsNotNull", node);
        public void Visit(InList node) => Record("InList", node);
        public void Visit(InSubquery node) => Record("InSubquery", node);
        public void Visit(Like node) => Record("Like", node);
        public void Visit(ILike node) => Record("ILike", node);
        public void Visit(Exists node) => Record("Exists", node);
        public void Visit(ScalarSubquery node) => Record("ScalarSubquery", node);
        public void Visit(Call node) => Record("Call", node);
        public void Visit(Aggregate node) => Record("Aggregate", node);
        public void Visit(CountFiltered node) => Record("CountFiltered", node);
        public void Visit(Cast node) => Record("Cast", node);
        public void Visit(CaseWhen node) => Record("CaseWhen", node);
        public void Visit(Aliased node) => Record("Aliased", node);
        public void Visit(AnyOf node) => Record("AnyOf", node);
        public void Visit(NullSafeEquals node) => Record("NullSafeEquals", node);
        public void Visit(Comparable node) => Record("Comparable", node);
        public void Visit(JsonKeyText node) => Record("JsonKeyText", node);
        public void Visit(JsonKeyExists node) => Record("JsonKeyExists", node);
        public void Visit(JsonArrayContainsString node) => Record("JsonArrayContainsString", node);
        public void Visit(JsonArrayContainsAny node) => Record("JsonArrayContainsAny", node);
        public void Visit(OptionRank node) => Record("OptionRank", node);
        public void Visit(MovedNumberOrEpochMillis node) => Record("MovedNumberOrEpochMillis", node);
        public void Visit(OrderTerm node) => Record("OrderTerm", node);
        public void Visit(OrderBy node) => Record("OrderBy", node);
        public void Visit(Where node) => Record("Where", node);
        public void Visit(Limit node) => Record("Limit", node);
        public void Visit(Offset node) => Record("Offset", node);
        public void Visit(InnerJoin node) => Record("InnerJoin", node);
        public void Visit(Select node) => Record("Select", node);
        public void Visit(Assignment node) => Record("Assignment", node);
        public void Visit(ValuesRow node) => Record("ValuesRow", node);
        public void Visit(Returning node) => Record("Returning", node);
        public void Visit(ConflictUpdate node) => Record("ConflictUpdate", node);
        public void Visit(Conflict node) => Record("Conflict", node);
        public void Visit(Insert node) => Record("Insert", node);
        public void Visit(Update node) => Record("Update", node);
        public void Visit(Delete node) => Record("Delete", node);
        public void Visit(Plus node) => Record("Plus", node);
        public void Visit(Coalesce node) => Record("Coalesce", node);
        public void Visit(ClockPlusMinutes node) => Record("ClockPlusMinutes", node);
        public void Visit(NamedLock node) => Record("NamedLock", node);
        public void Visit(JsonDocumentOrEmpty node) => Record("JsonDocumentOrEmpty", node);
        public void Visit(JsonConcat node) => Record("JsonConcat", node);
        public void Visit(JsonRemoveKey node) => Record("JsonRemoveKey", node);
        public void Visit(JsonObjectOf node) => Record("JsonObjectOf", node);
        public void Visit(JsonIntOrZero node) => Record("JsonIntOrZero", node);
        public void Visit(TypeRef node) => Record("TypeRef", node);
        public void Visit(RawSql node) => Record("RawSql", node);
        public void Visit(BareLit node) => Record("BareLit", node);
        public void Visit(NamedParam node) => Record("NamedParam", node);
        public void Visit(ColumnDef node) => Record("ColumnDef", node);
        public void Visit(TablePrimaryKey node) => Record("TablePrimaryKey", node);
        public void Visit(ForeignKeyDef node) => Record("ForeignKeyDef", node);
        public void Visit(CreateTable node) => Record("CreateTable", node);
        public void Visit(DropTable node) => Record("DropTable", node);
        public void Visit(AddColumn node) => Record("AddColumn", node);
        public void Visit(DropColumn node) => Record("DropColumn", node);
        public void Visit(RenameColumn node) => Record("RenameColumn", node);
        public void Visit(CreateIndex node) => Record("CreateIndex", node);
        public void Visit(DropIndex node) => Record("DropIndex", node);
        public void Visit(CreateView node) => Record("CreateView", node);
        public void Visit(DropView node) => Record("DropView", node);
        public void Visit(AddForeignKey node) => Record("AddForeignKey", node);
        public void Visit(DropForeignKey node) => Record("DropForeignKey", node);
        public void Visit(DataRow node) => Record("DataRow", node);
        public void Visit(InsertData node) => Record("InsertData", node);
        public void Visit(DeleteData node) => Record("DeleteData", node);
        public void Visit(ConvertForeignKeyAction node) => Record("ConvertForeignKeyAction", node);
        public void Visit(ConvertToLiveOnlyIndex node) => Record("ConvertToLiveOnlyIndex", node);
        public void Visit(SoftDeleteColumn node) => Record("SoftDeleteColumn", node);
        public void Visit(HistoryTableNode node) => Record("HistoryTableNode", node);
        public void Visit(RowHistoryFunction node) => Record("RowHistoryFunction", node);
        public void Visit(RowHistoryInstall node) => Record("RowHistoryInstall", node);
        public void Visit(Script node) => Record("Script", node);
        public void Visit(SessionLock node) => Record("SessionLock", node);
        public void Visit(ReconcileIndex node) => Record("ReconcileIndex", node);
        public void Visit(ReconcileIndexes node) => Record("ReconcileIndexes", node);
        public void Visit(HistoryChangedKeys node) => Record("HistoryChangedKeys", node);
        public void Visit(HistoryStateQuery node) => Record("HistoryStateQuery", node);
        public void Visit(HistoryRevertUpdate node) => Record("HistoryRevertUpdate", node);
        public void Visit(ExistingTablesQuery node) => Record("ExistingTablesQuery", node);
        public void Visit(ConstraintColumnsOf node) => Record("ConstraintColumnsOf", node);
        public void Visit(HistorySoftDeleteLookup node) => Record("HistorySoftDeleteLookup", node);
        public void Visit(SoftTrashBranch node) => Record("SoftTrashBranch", node);
        public void Visit(SoftTrashUnion node) => Record("SoftTrashUnion", node);
    }
}
