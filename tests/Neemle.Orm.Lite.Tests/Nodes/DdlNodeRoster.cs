using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.Nodes;

internal static class DdlNodeRoster
{
    private static readonly ColumnRef Column = new("c");

    private static readonly ColumnDef Definition = new("c", new TypeRef(ColumnType.Text), false, true, new StringLit("x"));

    internal static IReadOnlyList<Node> Samples { get; } =
    [
        new TypeRef(ColumnType.Decimal, 12, 4),
        new RawSql("postgres", "x"),
        new BareLit("1.5"),
        new NamedParam("key", 1L),
        Definition,
        new ForeignKeyDef("fk", [Column], "o", [new ColumnRef("id")], ReferentialAction.Cascade, ReferentialAction.NoAction),
        new CreateTable("t", [Definition], [new ForeignKeyDef("fk", [Column], "o", [new ColumnRef("id")])]),
        new DropTable("t"),
        new AddColumn("t", Definition),
        new DropColumn("t", "c"),
        new RenameColumn("t", "a", "b"),
        new CreateIndex("i", "t", [Column], true, new Where([new IsNull(Column)])),
        new DropIndex("i"),
        new CreateView("v", new Select([new IntLit(1)], new TableRef("t")), true),
        new DropView("v"),
        new AddForeignKey("fk", "t", [Column], "o", [new ColumnRef("id")], ReferentialAction.Cascade, ReferentialAction.NoAction),
        new DropForeignKey("fk", "t"),
        new DataRow([new IntLit(1)]),
        new InsertData("t", [Column], [new DataRow([new IntLit(1)])]),
        new DeleteData("t", "c", [new IntLit(1)]),
        new ConvertForeignKeyAction("t", "fk", [Column], "o", [new ColumnRef("id")], ReferentialAction.Cascade, ReferentialAction.NoAction, ReferentialAction.NoAction),
        new ConvertToLiveOnlyIndex("t", [Column], "deleted_at", "uq", "ix", true, "v1", false),
        new SoftDeleteColumn("t", "v1", false),
        new HistoryTableNode("t", "id", "deleted_at", "", [new ColumnRef("u")], "0123456789abcdef"),
        new RowHistoryFunction("a.b"),
        new RowHistoryInstall("a.b", []),
        new Script([new DropTable("t")]),
        new SessionLock(SessionLockOp.Wait, new IntLit(1)),
        new TablePrimaryKey([Column]),
        new ReconcileIndex(new CreateIndex("i", "t", [Column]), [Column], [Column]),
        new ReconcileIndexes([new ReconcileIndex(new CreateIndex("i", "t", [Column]), [Column])]),
    ];
}
