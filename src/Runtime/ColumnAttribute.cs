namespace Neemle.OrmLite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class ColumnAttribute : System.Attribute { public ColumnAttribute(string name) { } }
