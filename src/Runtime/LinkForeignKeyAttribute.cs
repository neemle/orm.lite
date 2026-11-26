using System;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class LinkForeignKeyAttribute(LinkForeignKeyEnd end, string name, ReferentialAction onDelete) : Attribute
{
    public LinkForeignKeyEnd End { get; } = end;

    public string Name { get; } = name;

    public ReferentialAction OnDelete { get; } = onDelete;
}
