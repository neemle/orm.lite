#nullable enable
using System;

namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CalculatedLowerAttribute(string sourceProperty) : Attribute
{
    public string SourceProperty { get; } = sourceProperty;
}
