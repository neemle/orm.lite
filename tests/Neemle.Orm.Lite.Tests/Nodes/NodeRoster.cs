using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests.Nodes;

internal static class NodeRoster
{
    private static readonly ColumnRef Column = new("c");

    private static readonly Param Bound = new(0, "v");

    private static readonly Select Sub = new([new IntLit(1)], new TableRef("t", "s0"));

    internal static IReadOnlyList<Node> Samples => [.. CoreSamples, .. DdlNodeRoster.Samples];

    private static IReadOnlyList<Node> CoreSamples { get; } =
    [
        new Star(),
        new NullLit(),
        new ClockNow(),
        new BoolLit(true),
        new IntLit(1),
        new StringLit("x"),
        new AliasRef("s0"),
        new TableRef("t", "s0"),
        Bound,
        new RowLock(RowLockStrength.Update),
        Column,
        new And(Column, Bound),
        new Or(Column, Bound),
        new Not(Column),
        new Group(Column),
        new Compare(CompareOp.Equal, Column, Bound),
        new IsNull(Column),
        new IsNotNull(Column),
        new InList(Column, [Bound]),
        new InSubquery(Column, Sub),
        new Like(Column, Bound, '~'),
        new ILike(Column, Bound, '~'),
        new Exists(Sub),
        new ScalarSubquery(Sub),
        new Call(Fn.Count, [new Star()]),
        new Aggregate(AggregateFn.Min, Column),
        new CountFiltered(Column),
        new Cast(Bound, CastType.Text),
        new CaseWhen(Column, new NullLit(), Column),
        new Aliased(Column, "a"),
        new AnyOf(Column, Bound),
        new NullSafeEquals(Column, Bound),
        new Comparable(Column, JsonValueOrder.Number),
        new JsonKeyText(Column, Bound),
        new JsonKeyExists(Column, Bound),
        new JsonArrayContainsString(Column, Bound, Bound),
        new JsonArrayContainsAny(Column, Bound, [Bound]),
        new OptionRank([Bound], Column),
        new MovedNumberOrEpochMillis(Column, Bound, Column),
        new OrderTerm(Column, false),
        new OrderBy([new OrderTerm(Column, true, NullsPlacement.Last)]),
        new Where([Column]),
        new Limit(1),
        new Offset(1),
        new InnerJoin(new TableRef("o", "a"), new Compare(CompareOp.Equal, Column, Bound)),
        Sub,
        new Assignment(Column, Bound),
        new ValuesRow([Bound]),
        new Returning([Column]),
        new ConflictUpdate([new Assignment(Column, Bound)], new IsNull(Column)),
        new Conflict([Column], new IsNull(Column), new ConflictUpdate([new Assignment(Column, Bound)])),
        new Insert(new TableRef("t"), [Column], [new ValuesRow([Bound])]),
        new Update(new TableRef("t"), [new Assignment(Column, Bound)]),
        new Delete(new TableRef("t")),
        new Plus(Column, Bound),
        new Coalesce(Column, Bound),
        new ClockPlusMinutes(Bound),
        new NamedLock(NamedLockKind.Exclusive, Bound),
        new JsonDocumentOrEmpty(Column),
        new JsonConcat(Column, Bound),
        new JsonRemoveKey(Column, Bound),
        new JsonObjectOf(Bound, Column),
        new JsonIntOrZero(Column),
        new HistoryChangedKeys(Column),
        new HistoryStateQuery("t", "id", "tenant_id", [Column], [new ColumnRef("u")], new Param(0, "t", "t"), new Param(1, "x", "id"), new Param(2, 1L, "k"), new Param(3, "w", "w")),
        new HistoryRevertUpdate(
            new HistoryStateQuery("t", "id", null, [Column], [], new Param(0, "t", "t"), new Param(1, "x", "id"), new Param(2, 1L, "k"), null),
            "deleted_at", [Column], "updated_at"),
        new ExistingTablesQuery(new Param(0, new[] { "t" }, "tables")),
        new ConstraintColumnsOf(ConstraintSource.Index, new Param(0, "n", "n")),
        new HistorySoftDeleteLookup(new Param(0, new[] { "t" }, "tables"), new Param(1, new[] { Guid.Empty }, "ids")),
        new SoftTrashBranch("e", "t", "id", "name", "deleted_at", "project_id", new Where([Column])),
        new SoftTrashUnion([new SoftTrashBranch("e", "t", "id", null, "deleted_at", null, new Where([Column]))], new Param(2, 1, "lim"), new Param(3, 0, "off")),
    ];
}
