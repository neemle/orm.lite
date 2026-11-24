namespace Neemle.Orm.Lite.Nodes;

public abstract record Leaf : Node
{
    public sealed override IReadOnlyList<Node> Children() => [];
}

public sealed record Star(string? Qualifier = null) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record NullLit : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record ClockNow : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record BoolLit(bool Value) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record IntLit(long Value) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record StringLit(string Value) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record AliasRef(string Alias) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record TableRef(string Table, string? Alias = null, bool Bare = false) : Leaf
{
    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record Param(int Ordinal, object? Value, string? Alias = null) : Leaf
{
    public bool Equals(Param? other) =>
        other is not null && Ordinal == other.Ordinal && Alias == other.Alias && ParamValues.AreEqual(Value, other.Value);

    public override int GetHashCode() => HashCode.Combine(Ordinal, Alias, ParamValues.HashOf(Value));

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

public sealed record RowLock(RowLockStrength Strength) : Leaf
{
    public override IReadOnlyList<Capability> Requires() =>
        Strength == RowLockStrength.KeyPreserving
            ? [Capability.RowLock, Capability.KeyPreservingRowLock]
            : [Capability.RowLock];

    public override void Accept(INodeVisitor visitor) => visitor.Visit(this);
}

internal static class ParamValues
{
    internal static bool AreEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left is string || right is string) return left.Equals(right);
        if (left is System.Collections.IEnumerable l && right is System.Collections.IEnumerable r)
        {
            var le = l.GetEnumerator();
            var re = r.GetEnumerator();
            while (true)
            {
                var more = le.MoveNext();
                if (more != re.MoveNext()) return false;
                if (!more) return true;
                if (!AreEqual(le.Current, re.Current)) return false;
            }
        }
        return left.Equals(right);
    }

    internal static int HashOf(object? value)
    {
        if (value is null) return 0;
        if (value is string text) return text.GetHashCode();
        if (value is System.Collections.IEnumerable sequence)
        {
            var hash = new HashCode();
            foreach (var item in sequence) hash.Add(HashOf(item));
            return hash.ToHashCode();
        }
        return value.GetHashCode();
    }
}
