namespace Neemle.OrmLite;

public enum OrmLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None,
}

public enum TransactionPhase
{
    Begin,
    Commit,
    Rollback,
}

public sealed record LoggedParameter(string Name, string? Value);

public sealed record StatementEvent(
    OrmLogLevel Level,
    string Provider,
    StatementKind Kind,
    string Text,
    int ParameterCount,
    IReadOnlyList<LoggedParameter>? Parameters,
    TimeSpan Elapsed,
    int? RowsAffected,
    long ConnectionId,
    long? TransactionId,
    bool Slow,
    Exception? Error);

public sealed record TransactionEvent(
    OrmLogLevel Level,
    string Provider,
    TransactionPhase Phase,
    long ConnectionId,
    long TransactionId,
    TimeSpan Elapsed,
    int StatementCount);

public interface IOrmDiagnosticsSink
{
    void OnStatement(StatementEvent statement);

    void OnTransaction(TransactionEvent transaction);
}
