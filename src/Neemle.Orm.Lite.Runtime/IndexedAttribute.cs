namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class IndexedAttribute : System.Attribute { public bool Unique { get; set; } }
