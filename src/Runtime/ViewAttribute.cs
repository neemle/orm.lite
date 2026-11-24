namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ViewAttribute : Attribute
{
    public ViewAttribute(string name) { }
}
