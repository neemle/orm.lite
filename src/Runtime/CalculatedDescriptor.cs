namespace Neemle.OrmLite;

public sealed class CalculatedDescriptor(string property, string alias, string sourceColumn)
{
    public string Property { get; } = property;

    public string Alias { get; } = alias;

    public string SourceColumn { get; } = sourceColumn;
}
