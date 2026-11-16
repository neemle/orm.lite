namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public sealed class IndexAttribute : System.Attribute { public IndexAttribute(params string[] columns) { } public bool Unique { get; set; } public string? Name { get; set; } public bool LiveOnly { get; set; } }
