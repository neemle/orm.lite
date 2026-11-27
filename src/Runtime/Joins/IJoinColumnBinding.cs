namespace Neemle.OrmLite;

internal interface IJoinColumnBinding
{
    string Property { get; }
    ColumnDescriptor Column { get; }
    bool Calculated { get; }
    Type ValueType { get; }
    Delegate Reader { get; }
}
