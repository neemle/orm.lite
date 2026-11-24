using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Property, AllowMultiple = true)]
public sealed class IndexAttribute : System.Attribute
{
    public IndexAttribute(params string[] terms) { }

    public bool Unique { get; set; }

    public string? Name { get; set; }

    public bool LiveOnly { get; set; }

    public string? Where { get; set; }

    public bool Descending { get; set; }

    public NullsPlacement Nulls { get; set; }

    public string? Collation { get; set; }

    public bool Lower { get; set; }
}
