using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class JoinRegisteredColumn<TValue>(string property, ColumnDescriptor column,
    bool calculated, Func<DbDataReader, int, TValue> read) : IJoinColumnBinding
{
    public string Property { get; } = property;
    public ColumnDescriptor Column { get; } = column;
    public bool Calculated { get; } = calculated;
    public Type ValueType => typeof(TValue);
    public Delegate Reader => Read;
    internal Func<DbDataReader, int, TValue> Read { get; } = read;

    public JoinResultEntry Decode(JoinDecodeInput input)
    {
        if (input.Slot is not JoinSlot<TValue> slot) throw new JoinReadException(JoinError.MappingFailure);
        var value = input.Present && !input.IsNull ? Read(input.Reader, input.Ordinal) : default!;
        return JoinResultEntry.For(slot, new JoinCell<TValue>(input.Present, input.IsNull, value));
    }
}
