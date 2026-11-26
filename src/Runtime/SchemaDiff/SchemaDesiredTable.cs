namespace Neemle.OrmLite;

internal sealed record SchemaDesiredTable(
    string Name, IReadOnlyList<ColumnDescriptor> Columns, bool IsView, bool ComplexEffects);
