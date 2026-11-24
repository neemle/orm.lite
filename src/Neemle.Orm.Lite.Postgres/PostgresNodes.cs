using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Postgres;

public interface IPostgresVisitor
{
    void Visit(PgName node);
    void Visit(PgRelation node);
    void Visit(PgCall node);
    void Visit(PgCast node);
    void Visit(PgNullable node);
    void Visit(PgNegate node);
    void Visit(PgAll node);
    void Visit(PgExists node);
    void Visit(PgScalar node);
    void Visit(PgQuery node);
    void Visit(PgDeclare node);
    void Visit(PgBlock node);
    void Visit(PgIf node);
    void Visit(PgPerform node);
    void Visit(PgRaise node);
    void Visit(PgExecuteFormat node);
    void Visit(PgComment node);
    void Visit(PgCreateTrigger node);
    void Visit(PgSetLocal node);
    void Visit(PgPlainIndex node);
    void Visit(PgReplaceConstraint node);
    void Visit(PgAnyArray node);
    void Visit(PgExecuteStatement node);
    void Visit(PgDataLiteral node);
}

public abstract record PgNode : Node
{
    public sealed override void Accept(INodeVisitor visitor)
    {
        if (visitor is IPostgresVisitor typed) Dispatch(typed);
        else throw new NotSupportedException($"{Name} is a PostgreSQL node and only the PostgreSQL serializer writes it.");
    }

    protected abstract void Dispatch(IPostgresVisitor visitor);
}

public abstract record PgLeaf : PgNode
{
    public sealed override IReadOnlyList<Node> Children() => [];
}

public sealed record PgName(string Text) : PgLeaf
{
    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgRelation(string Relation, string? Alias = null) : PgLeaf
{
    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgCall(string Function, NodeList Arguments) : PgNode
{
    public override IReadOnlyList<Node> Children() => [.. Arguments];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgCast(Node Operand, string Type) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Operand];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgNullable(Node Type) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Type];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgNegate(Node Operand) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Operand];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgAll(NodeList Terms) : PgNode
{
    public override IReadOnlyList<Node> Children() => [.. Terms];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgExists(bool Negated, PgQuery Query) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Query];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgScalar(PgQuery Query) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Query];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgQuery(
    NodeList Columns,
    Node From,
    Where? Where = null,
    string? Into = null,
    NodeList? GroupBy = null,
    Node? Having = null,
    int? Limit = null) : PgNode
{
    public override IReadOnlyList<Node> Children() =>
        [.. Columns, From, .. Slots(Where), .. (GroupBy ?? NodeList.Empty), .. Slots(Having)];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgDeclare(string Variable, string Type) : PgLeaf
{
    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgBlock(string Tag, NodeList Declarations, NodeList Statements) : PgNode
{
    public override IReadOnlyList<Node> Children() => [.. Declarations, .. Statements];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgIf(Node Condition, NodeList Then, NodeList Otherwise) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Condition, .. Then, .. Otherwise];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgPerform(Node Call) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Call];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgRaise(Node Message, NodeList Arguments) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Message, .. Arguments];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgExecuteFormat(Node Template, Node Argument) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Template, Argument];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgComment(string ObjectKind, Node Subject, Node? On, Node Text) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Subject, .. Slots(On), Text];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgCreateTrigger(
    string TriggerName,
    string Event,
    string Table,
    bool OnlyWhenChanged,
    string Function,
    NodeList Arguments) : PgNode
{
    public override IReadOnlyList<Node> Children() => [.. Arguments];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgSetLocal(string Setting, Node Value) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Value];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgPlainIndex(string IndexName, string Table, NodeList Columns) : PgNode
{
    public override IReadOnlyList<Node> Children() => [.. Columns];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgReplaceConstraint(string Table, string Dropped, AddForeignKey Added) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Added];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgDataLiteral(string Text) : PgLeaf
{
    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgAnyArray(Node Operand, NodeList Items) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Operand, .. Items];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}

public sealed record PgExecuteStatement(Node Statement) : PgNode
{
    public override IReadOnlyList<Node> Children() => [Statement];

    protected override void Dispatch(IPostgresVisitor visitor) => visitor.Visit(this);
}
