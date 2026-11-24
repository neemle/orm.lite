namespace Neemle.OrmLite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class ManyToManyAttribute : System.Attribute { public ManyToManyAttribute(string joinTable, string thisKey, string otherKey) { } }
