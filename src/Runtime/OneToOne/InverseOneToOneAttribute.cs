namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class InverseOneToOneAttribute(string ownerNavigation) : Attribute
{
    public string OwnerNavigation { get; } = ownerNavigation;
}
