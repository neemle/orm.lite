namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class OneToOneAttribute(string foreignKey, string inverseNavigation) : Attribute
{
    public string ForeignKey { get; } = foreignKey;
    public string InverseNavigation { get; } = inverseNavigation;
}
