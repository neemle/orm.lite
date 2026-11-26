namespace Neemle.OrmLite;

internal static class StreamParameterValues
{
    internal static object? Copy(object? value)
    {
        if (value is null || Scalar(value)) return value;
        if (value is not Array array || array.Rank != 1 || array.GetLowerBound(0) != 0)
            throw new QueryStreamException(QueryStreamError.FrozenParameterUnsupported);
        if (!ScalarType(array.GetType().GetElementType()!))
            throw new QueryStreamException(QueryStreamError.FrozenParameterUnsupported);
        var copy = (Array)array.Clone();
        foreach (var item in copy)
            if (item is not null && !Scalar(item))
                throw new QueryStreamException(QueryStreamError.FrozenParameterUnsupported);
        return copy;
    }

    private static readonly HashSet<Type> Scalars =
    [
        typeof(DBNull), typeof(string), typeof(bool), typeof(char), typeof(byte), typeof(sbyte), typeof(short),
        typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double),
        typeof(decimal), typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
        typeof(TimeSpan), typeof(Jsonb),
    ];

    private static bool Scalar(object value) => ScalarType(value.GetType());

    private static bool ScalarType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum || Scalars.Contains(type);
    }

    internal static Statement Copy(Statement statement) => new(statement.Text,
        statement.Parameters.Select(p => new StatementParameter(p.Ordinal, p.Name, Copy(p.Value))).ToArray(),
        statement.Kind);
}
