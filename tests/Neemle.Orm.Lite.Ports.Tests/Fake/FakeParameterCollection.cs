using System.Collections;
using System.Data.Common;

namespace Neemle.Orm.Lite.Ports.Tests.Fake;

public sealed class FakeParameterCollection : DbParameterCollection
{
    private readonly List<FakeParameter> _items = new();

    private readonly object _root = new();

    public IReadOnlyList<FakeParameter> Items => _items;

    public override int Count => _items.Count;

    public override object SyncRoot => _root;

    public override bool IsFixedSize => false;

    public override bool IsReadOnly => false;

    public override bool IsSynchronized => false;

    public override int Add(object value)
    {
        _items.Add(Cast(value));
        return _items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values) Add(value);
    }

    public override void Clear() => _items.Clear();

    public override bool Contains(object value) => IndexOf(value) >= 0;

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => ((IEnumerable)_items).GetEnumerator();

    public override int IndexOf(object value) => _items.IndexOf(value as FakeParameter);

    public override int IndexOf(string parameterName) =>
        _items.FindIndex(p => string.Equals(p.ParameterName, parameterName, StringComparison.Ordinal));

    public override void Insert(int index, object value) => _items.Insert(index, Cast(value));

    public override void Remove(object value) => _items.Remove(value as FakeParameter);

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _items[index];

    protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _items[index] = Cast(value);

    protected override void SetParameter(string parameterName, DbParameter value) =>
        _items[IndexOf(parameterName)] = Cast(value);

    private static FakeParameter Cast(object value) =>
        value as FakeParameter
        ?? throw new ArgumentException("the fake collection holds FakeParameter only.", nameof(value));
}
