namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class ReferenceAttribute : System.Attribute { public ReferenceAttribute(string? foreignKey = null) { } }
