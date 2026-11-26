#nullable enable
using System;
using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class LinkedRead<T>(
    EntityDescriptor owner,
    string navigation,
    object? key,
    EntityDescriptor target,
    Func<DbDataReader, T> map)
{
    public EntityDescriptor Owner { get; } = owner;

    public string Navigation { get; } = navigation;

    public object? Key { get; } = key;

    public EntityDescriptor Target { get; } = target;

    public Func<DbDataReader, T> Map { get; } = map;
}
