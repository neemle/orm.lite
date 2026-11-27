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
    Exception? Error)
{
    /// <summary>
    /// The maintenance operation a <see cref="StatementKind.Maintenance"/> statement ran for (a
    /// <see cref="MaintenanceOperation"/> name); null for every other statement.
    /// </summary>
    public string? Operation { get; init; }
}

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
