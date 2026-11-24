namespace Neemle.Orm.Lite;

public enum StatementKind
{
    Read,
    Write,
    Ddl,
}

public sealed record StatementParameter(int Ordinal, string Name, object? Value);

public sealed record Statement(string Text, IReadOnlyList<StatementParameter> Parameters, StatementKind Kind)
{
    public IReadOnlyList<object?> Values => Parameters.Select(p => p.Value).ToList();
}

public sealed record SerializedStatement(string Text, IReadOnlyList<StatementParameter> Parameters);
