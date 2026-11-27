namespace Neemle.OrmLite;

public readonly struct JoinCell<TValue>
{
    private readonly bool _sourcePresent;
    private readonly bool _hasValue;
    private readonly TValue _value;

    internal JoinCell(bool sourcePresent, bool isNull, TValue value)
    {
        _sourcePresent = sourcePresent;
        _hasValue = HasValue(sourcePresent, isNull, value);
        _value = _hasValue ? value : default!;
    }

    public bool SourcePresent => _sourcePresent;
    public bool IsNull => !_hasValue;
    public TValue Value => _hasValue ? _value : throw new JoinReadException(JoinError.MissingValue);

    private static bool HasValue(bool sourcePresent, bool isNull, TValue value)
    {
        if (isNull) return false;
        if (!sourcePresent) throw new JoinReadException(JoinError.MappingFailure);
        if (value is null) throw new JoinReadException(JoinError.MappingFailure);
        return true;
    }
}
