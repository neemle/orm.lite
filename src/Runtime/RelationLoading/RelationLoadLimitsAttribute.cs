namespace Neemle.OrmLite;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RelationLoadLimitsAttribute : Attribute
{
    public int MaxDepth { get; set; }
    public int MaxPaths { get; set; }
    public int MaxNodes { get; set; }
    public int MaxRoots { get; set; }
    public int MaxRelatedRows { get; set; }
    public int MaxStatements { get; set; }
}
