using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

public interface ISqliteHistoryVisitor
{
    void Visit(SqliteHistoryStore node);
    void Visit(SqliteHistoryTrigger node);
}

public abstract record SqliteHistoryNode : Node
{
    public sealed override IReadOnlyList<Node> Children() => [];

    public sealed override void Accept(INodeVisitor visitor)
    {
        if (visitor is ISqliteHistoryVisitor typed) Dispatch(typed);
        else throw new NotSupportedException($"{Name} is a SQLite node and only the SQLite serializer writes it.");
    }

    protected abstract void Dispatch(ISqliteHistoryVisitor visitor);
}

public sealed record SqliteHistoryStore : SqliteHistoryNode
{
    protected override void Dispatch(ISqliteHistoryVisitor visitor) => visitor.Visit(this);
}

public enum SqliteTriggerKind
{
    Update,
    Delete,
}

public sealed record SqliteHistoryTrigger(
    SqliteTriggerKind Kind,
    string Table,
    string KeyColumn,
    string SoftColumn,
    string? TenantColumn,
    IReadOnlyList<string> Versioned) : SqliteHistoryNode
{
    public bool Equals(SqliteHistoryTrigger? other) =>
        other is not null
        && Kind == other.Kind && Table == other.Table && KeyColumn == other.KeyColumn && SoftColumn == other.SoftColumn
        && TenantColumn == other.TenantColumn && Versioned.SequenceEqual(other.Versioned);

    public override int GetHashCode() => HashCode.Combine(Kind, Table, KeyColumn, SoftColumn, TenantColumn, Versioned.Count);

    protected override void Dispatch(ISqliteHistoryVisitor visitor) => visitor.Visit(this);
}
