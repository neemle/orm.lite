using System;
using System.Collections.Immutable;

namespace Neemle.OrmLite.Generator;

/// <summary>The collection builder of <see cref="EquatableArray{T}"/>: it makes collection expressions work.</summary>
internal static class EquatableArray
{
    internal static EquatableArray<T> Create<T>(ReadOnlySpan<T> items) =>
        new(ImmutableArray.Create(items.ToArray()));
}
