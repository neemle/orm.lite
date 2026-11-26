namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RelationLoadAttribute(RelationLoadMode mode) : Attribute
{
    public RelationLoadMode Mode { get; } = mode;
}
