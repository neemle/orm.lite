using System;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ForeignKeyAttribute(string name, ReferentialAction onDelete) : Attribute
{
    public string Name { get; } = name;

    public ReferentialAction OnDelete { get; } = onDelete;
}
