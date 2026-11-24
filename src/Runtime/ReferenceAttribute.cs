namespace Neemle.OrmLite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class ReferenceAttribute : System.Attribute { public ReferenceAttribute(string? foreignKey = null) { } }
