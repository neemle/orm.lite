using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

public interface ISqliteVisitor
{
    void Visit(SqlitePragma node);
    void Visit(SqliteStored node);
    void Visit(SqliteCreateTableFrom node);
    void Visit(SqliteCopyRows node);
    void Visit(SqliteRenameTable node);
    void Visit(SqliteDropObject node);
    void Visit(SqliteDuplicateKeys node);
}

public abstract record SqliteNode : Node
{
    public sealed override void Accept(INodeVisitor visitor)
    {
        if (visitor is ISqliteVisitor typed) Dispatch(typed);
        else throw new NotSupportedException($"{Name} is a SQLite node and only the SQLite serializer writes it.");
    }

    protected abstract void Dispatch(ISqliteVisitor visitor);
}

public abstract record SqliteLeaf : SqliteNode
{
    public sealed override IReadOnlyList<Node> Children() => [];
}

public sealed record SqlitePragma(string Pragma, string? Argument = null, string? Value = null) : SqliteLeaf
{
    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteStored(string Text) : SqliteLeaf
{
    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteCreateTableFrom(string Table, NodeList Definitions, string Tail) : SqliteNode
{
    public override IReadOnlyList<Node> Children() => [.. Definitions];

    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteCopyRows(string From, string To, NodeList Columns) : SqliteNode
{
    public override IReadOnlyList<Node> Children() => [.. Columns];

    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteRenameTable(string From, string To) : SqliteLeaf
{
    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteDropObject(string Kind, string Object) : SqliteLeaf
{
    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}

public sealed record SqliteDuplicateKeys(string Table, NodeList KeyTerms, NodeList KeyColumns) : SqliteNode
{
    public override IReadOnlyList<Node> Children() => [.. KeyTerms, .. KeyColumns];

    protected override void Dispatch(ISqliteVisitor visitor) => visitor.Visit(this);
}
