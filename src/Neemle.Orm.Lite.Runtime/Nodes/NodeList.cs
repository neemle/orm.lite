using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neemle.Orm.Lite.Nodes;

[CollectionBuilder(typeof(NodeList), nameof(Create))]
public sealed class NodeList : IReadOnlyList<Node>, IEquatable<NodeList>
{
    private readonly Node[] _items;

    public static readonly NodeList Empty = new([]);

    private NodeList(Node[] items) => _items = items;

    public static NodeList Create(ReadOnlySpan<Node> items) => new(items.ToArray());

    public static NodeList Of(IEnumerable<Node> items) => new(items.ToArray());

    public int Count => _items.Length;

    public Node this[int index] => _items[index];

    public IEnumerator<Node> GetEnumerator() => ((IEnumerable<Node>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(NodeList? other)
    {
        if (other is null || other._items.Length != _items.Length) return false;
        for (var i = 0; i < _items.Length; i++)
            if (!_items[i].Equals(other._items[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is NodeList other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _items) hash.Add(item);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var sb = new StringBuilder().Append('[');
        for (var i = 0; i < _items.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_items[i]);
        }
        return sb.Append(']').ToString();
    }
}
