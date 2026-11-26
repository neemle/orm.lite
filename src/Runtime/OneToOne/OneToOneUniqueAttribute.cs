namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class OneToOneUniqueAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
