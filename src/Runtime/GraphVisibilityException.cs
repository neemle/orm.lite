namespace Neemle.OrmLite;

public sealed class GraphVisibilityException : NotSupportedException
{
    internal GraphVisibilityException(string path, string property)
        : base("Graph traversal requires a masked mapped key.")
    { Path = path; Property = property; }

    public string Path { get; }
    public string Property { get; }
}
