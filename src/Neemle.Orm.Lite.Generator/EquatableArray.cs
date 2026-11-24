using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Neemle.Orm.Lite;

/// <summary>
/// An immutable array that compares by its items. The incremental pipeline decides from equality whether a step
/// produced something new, and an ImmutableArray compares by reference, so a model that holds one never compares
/// equal to the model of the previous run and every edit would regenerate all output. The default value is an empty
/// array. Collection expressions build it through <see cref="EquatableArray"/>.
/// </summary>
[CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public bool IsEmpty => Length == 0;

    public bool IsDefaultOrEmpty => Length == 0;

    public T this[int index] => _items[index];

    public static implicit operator EquatableArray<T>(ImmutableArray<T> items) => new(items);

    public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.AsImmutable();

    public ImmutableArray<T> AsImmutable() => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public bool Contains(T item) => AsImmutable().Contains(item);

    public bool Equals(EquatableArray<T> other)
    {
        var left = AsImmutable();
        var right = other.AsImmutable();
        if (left.Length != right.Length) return false;
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < left.Length; i++)
            if (!comparer.Equals(left[i], right[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in AsImmutable()) hash.Add(item);
        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)AsImmutable()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
