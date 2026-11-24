namespace Neemle.Orm.Lite.Nodes;

public enum ConstraintSource
{
    Constraint,
    Index,
}

public sealed record HistoryChangedKeys(Node Document) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers];

    public override IReadOnlyList<Node> Children() => [Document];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record HistoryStateQuery(
    string Table,
    string KeyColumn,
    string? TenantColumn,
    NodeList Columns,
    NodeList Unversioned,
    Param SourceTable,
    Param RowId,
    Param Version,
    Param? Tenant) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers];

    public override IReadOnlyList<Node> Children() =>
        [.. Columns, .. Unversioned, SourceTable, RowId, Version, .. Slots(Tenant)];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record HistoryRevertUpdate(
    HistoryStateQuery State,
    string SoftColumn,
    NodeList Assignments,
    string? StampColumn) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers];

    public override IReadOnlyList<Node> Children() => [State, .. Assignments];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ExistingTablesQuery(Param Tables) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.CatalogGuard];

    public override IReadOnlyList<Node> Children() => [Tables];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ConstraintColumnsOf(ConstraintSource Source, Param Target) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.CatalogGuard];

    public override IReadOnlyList<Node> Children() => [Target];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record HistorySoftDeleteLookup(Param Tables, Param Ids) : Node
{
    public override IReadOnlyList<Capability> Requires() => [Capability.RowHistoryTriggers];

    public override IReadOnlyList<Node> Children() => [Tables, Ids];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record SoftTrashBranch(
    string Entity,
    string Table,
    string KeyColumn,
    string? LabelColumn,
    string SoftColumn,
    string? ProjectColumn,
    Where Filter) : Node
{
    public override IReadOnlyList<Node> Children() => [Filter];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record SoftTrashUnion(NodeList Branches, Param Limit, Param Offset) : Node
{
    public override IReadOnlyList<Node> Children() => [.. Branches, Limit, Offset];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}
