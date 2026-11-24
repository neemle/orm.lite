namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RequiresCapabilityAttribute(Capability capability) : Attribute
{
    public Capability Capability { get; } = capability;
}
