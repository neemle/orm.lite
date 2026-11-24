namespace Neemle.OrmLite.Nodes;

public enum ColumnType
{
    Uuid,
    Text,
    Int32,
    Int64,
    Bool,
    Double,
    Decimal,
    Timestamp,
    Date,
    Json,
}

public enum ReferentialAction
{
    NoAction,
    Restrict,
    Cascade,
    SetNull,
}

public enum OpClass
{
    Reversible,
    Lossy,
    Unknown,
}

public enum SessionLockOp
{
    Wait,
    TryAcquire,
    Release,
}

public sealed record TypeRef(ColumnType Kind, int Precision = 0, int Scale = 0) : Leaf
{
    public override IReadOnlyList<Capability> Requires() =>
        Kind switch
        {
            ColumnType.Json => [Capability.JsonDocument],
            ColumnType.Decimal => [Capability.ExactDecimal],
            _ => [],
        };

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record RawSql(string? ProviderId, string Text) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record BareLit(string Text, bool ExactDecimal = false) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record NamedParam(string Parameter, object? Value) : Leaf
{
    public bool Equals(NamedParam? other) =>
        other is not null && Parameter == other.Parameter && ParamValues.AreEqual(Value, other.Value);

    public override int GetHashCode() => HashCode.Combine(Parameter, ParamValues.HashOf(Value));

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ColumnDef(string Column, Node Type, bool Nullable = true, bool PrimaryKey = false, Node? Default = null) : Node
{
    public override IReadOnlyList<Node> Children() => Slots(Type, Default);

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ForeignKeyDef(
    string Constraint,
    NodeList Columns,
    string ReferencedTable,
    NodeList ReferencedColumns,
    ReferentialAction? OnDelete = null,
    ReferentialAction? OnUpdate = null) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Columns, .. ReferencedColumns];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record CreateTable(string Table, NodeList Columns, NodeList? Constraints = null) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Columns, .. Constraints ?? NodeList.Empty];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DropTable(string Table) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record AddColumn(string Table, ColumnDef Column, bool IfNotExists = true) : Node
{
    public override IReadOnlyList<Node> Children() => [Column];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DropColumn(string Table, string Column, bool IfExists = true) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record RenameColumn(string Table, string From, string To) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record CreateIndex(string Index, string Table, NodeList Terms, bool Unique = false, Where? Where = null) : Node
{
    public override IReadOnlyList<Capability> Requires()
    {
        var needs = new List<Capability>();
        if (Where is not null) needs.Add(Capability.PartialIndex);
        if (Terms.OfType<OrderTerm>().Any(t => t.Nulls != NullsPlacement.Unspecified)) needs.Add(Capability.IndexNullsOrdering);
        if (Terms.OfType<OrderTerm>().Any(t => t.Collation is not null)) needs.Add(Capability.IndexCollation);
        if (Terms.Any(IsExpression)) needs.Add(Capability.IndexExpression);
        return needs;
    }

    private static bool IsExpression(Node term) => (term is OrderTerm order ? order.Operand : term) is Call;

    public override IReadOnlyList<Node> Children() => [.. Terms, .. Slots(Where)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DropIndex(string Index, bool IfExists = true) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record CreateView(string View, Select Query, bool Replace = false) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.ObjectMarker];

    public override IReadOnlyList<Node> Children() => [Query];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DropView(string View, bool IfExists = true) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record AddForeignKey(
    string Constraint,
    string Table,
    NodeList Columns,
    string ReferencedTable,
    NodeList ReferencedColumns,
    ReferentialAction? OnDelete = null,
    ReferentialAction? OnUpdate = null) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Columns, .. ReferencedColumns];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DropForeignKey(string Constraint, string Table) : Leaf
{
    public override IReadOnlyList<Capability> Requires() => [Capability.AlterConstraint];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DataRow(NodeList Values) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Values];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record InsertData(string Table, NodeList Columns, NodeList Rows) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Columns, .. Rows];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record DeleteData(string Table, string Column, NodeList Values) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Values];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ConvertForeignKeyAction(
    string Table,
    string Constraint,
    NodeList Columns,
    string ReferencedTable,
    NodeList ReferencedColumns,
    ReferentialAction Target,
    ReferentialAction Restore,
    ReferentialAction OnUpdate) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.AlterConstraint];

    public override IReadOnlyList<Node> Children() => [.. Columns, .. ReferencedColumns];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ConvertToLiveOnlyIndex(
    string Table,
    NodeList KeyTerms,
    string SoftColumn,
    string TotalIndex,
    string LiveIndex,
    bool LiveIsDeclared,
    string Version,
    bool Revert) : Node
{
    public override IReadOnlyList<Capability> Requires() =>
        [Capability.PartialIndex, Capability.AlterConstraint, Capability.ObjectMarker];

    public override IReadOnlyList<Node> Children() => [.. KeyTerms];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record SoftDeleteColumn(string Table, string Version, bool Remove) : Leaf
{
    public override IReadOnlyList<Capability> Requires() => [Capability.ObjectMarker];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record HistoryTableNode(
    string Table,
    string KeyColumn,
    string SoftColumn,
    string TenantColumn,
    NodeList Unversioned,
    string ArgumentsHash) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Unversioned];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record RowHistoryFunction(string ActorSetting) : Leaf
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record RowHistoryInstall(string ActorSetting, NodeList Tables) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers, Capability.ObjectMarker];

    public override IReadOnlyList<Node> Children() => [.. Tables];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Script(NodeList Statements) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Statements];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record SessionLock(SessionLockOp Op, Node Key) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.SessionLock];

    public override IReadOnlyList<Node> Children() => [Key];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ReconcileIndex(CreateIndex Create, NodeList GuardColumns, NodeList? CollisionKeys = null) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.CatalogGuard, .. Create.Requires()];

    public override IReadOnlyList<Node> Children() => [Create, .. GuardColumns, .. (CollisionKeys ?? NodeList.Empty)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ReconcileIndexes(NodeList Indexes) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.CatalogGuard];

    public override IReadOnlyList<Node> Children() => [.. Indexes];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record TablePrimaryKey(NodeList Columns) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Columns];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
