using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace Neemle.OrmLite;

internal sealed class OrmDiagnostics
{
    internal const string SourceName = "Neemle.OrmLite";

    // The activity tag that names the maintenance operation of a StatementKind.Maintenance statement.
    internal const string OperationTag = "orm.operation";

    internal static readonly ActivitySource Source = new(SourceName);

    private static int s_active;

    private static long s_ids;

    private readonly string _provider;

    private readonly IOrmDiagnosticsSink? _sink;

    private readonly OrmLogLevel _statementLevel;

    private readonly TimeSpan _slow;

    private readonly OrmLogLevel _slowLevel;

    private readonly OrmLogLevel _transactionLevel;

    private readonly double _sampleRate;

    private readonly bool _logParameters;

    private readonly int _parameterMaxLength;

    private OrmDiagnostics(string provider, OrmLiteDiagnosticsOptions? options)
    {
        _provider = provider;
        if (options is null || !options.Reports) return;
        _sink = options.Sink;
        _statementLevel = options.StatementLevel;
        _slow = options.SlowThreshold;
        _slowLevel = options.SlowLevel;
        _transactionLevel = options.TransactionLevel;
        _sampleRate = options.SampleRate;
        _logParameters = options.LogParameters;
        _parameterMaxLength = options.ParameterMaxLength;
        Interlocked.Exchange(ref s_active, 1);
    }

    internal static bool Possible => Volatile.Read(ref s_active) != 0 || Source.HasListeners();

    internal static OrmDiagnostics Create(ISqlProvider provider, OrmLiteDiagnosticsOptions options)
    {
        options.Validate();
        return new OrmDiagnostics(provider.Id, options);
    }

    internal static OrmDiagnostics Disabled(ISqlProvider provider) => new(provider.Id, null);

    internal bool Active => _sink is not null;

    internal static long NextId() => Interlocked.Increment(ref s_ids);

    internal async Task<T> RunAsync<T>(
        DbCommand command, StatementKind kind, GatedDataSource.Lease lease,
        Func<DbCommand, CancellationToken, Task<T>> run, Func<T, int?> rows, CancellationToken ct, string? operation = null)
    {
        using var activity = Source.StartActivity("orm.statement", ActivityKind.Client);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("db.system", _provider);
            activity.SetTag("db.statement", command.CommandText);
            if (operation is not null) activity.SetTag(OperationTag, operation);
        }
        if (_sink is null) return await run(command, ct);

        var start = Stopwatch.GetTimestamp();
        Exception? error = null;
        int? affected = null;
        try
        {
            var result = await run(command, ct);
            affected = rows(result);
            return result;
        }
        catch (Exception e)
        {
            error = e;
            throw;
        }
        finally
        {
            Report(command, kind, lease, Stopwatch.GetElapsedTime(start), affected, error, operation);
        }
    }

    internal async Task<T> RunGraphSaveAsync<T>(GraphSaveDiagnosticRun request,
        Func<Task<T>> run, Func<T, int?> rows)
    {
        using var activity = Source.StartActivity("orm.graph-save", ActivityKind.Client);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("db.system", _provider);
            activity.SetTag("db.statement", "graph-save");
        }
        if (_sink is null) return await run();
        var start = Stopwatch.GetTimestamp();
        var failed = false;
        int? affected = null;
        try
        {
            var result = await run();
            affected = rows(result);
            return result;
        }
        catch { failed = true; throw; }
        finally
        {
            ReportGraphSave(request, new GraphSaveDiagnosticOutcome(Stopwatch.GetElapsedTime(start), affected, failed));
        }
    }

    private void ReportGraphSave(GraphSaveDiagnosticRun request, GraphSaveDiagnosticOutcome outcome)
    {
        var trace = request.Lease.Transaction;
        trace?.Count();
        var slow = _slow > TimeSpan.Zero && _slowLevel != OrmLogLevel.None && outcome.Elapsed >= _slow;
        OrmLogLevel? level = outcome.Failed ? OrmLogLevel.Error : null;
        if (slow) level = Raise(level, _slowLevel);
        if (_statementLevel != OrmLogLevel.None && Sampled()) level = Raise(level, _statementLevel);
        if (level is not { } reported) return;
        try
        {
            _sink!.OnStatement(new StatementEvent(reported, _provider, request.Kind, "graph-save",
                request.Command.Parameters.Count, null, outcome.Elapsed, outcome.Affected, request.Lease.ConnectionId,
                trace?.Id, slow, outcome.Failed ? new GraphSaveDiagnosticError() : null));
        }
        catch { }
    }

    internal void Emit(TransactionEvent transaction)
    {
        if (_sink is null || _transactionLevel == OrmLogLevel.None) return;
        try
        {
            _sink.OnTransaction(transaction with { Level = _transactionLevel });
        }
        catch
        {
        }
    }

    internal string Provider => _provider;

    private void Report(DbCommand command, StatementKind kind, GatedDataSource.Lease lease, TimeSpan elapsed, int? affected, Exception? error,
        string? operation)
    {
        var trace = lease.Transaction;
        trace?.Count();
        var slow = _slow > TimeSpan.Zero && _slowLevel != OrmLogLevel.None && elapsed >= _slow;
        OrmLogLevel? level = error is null ? null : OrmLogLevel.Error;
        if (slow) level = Raise(level, _slowLevel);
        if (_statementLevel != OrmLogLevel.None && Sampled()) level = Raise(level, _statementLevel);
        if (level is not { } reported) return;

        try
        {
            _sink!.OnStatement(new StatementEvent(
                reported, _provider, kind, command.CommandText, command.Parameters.Count,
                _logParameters ? Describe(command.Parameters) : null,
                elapsed, affected, lease.ConnectionId, trace?.Id, slow, error) { Operation = operation });
        }
        catch
        {
        }
    }

    private bool Sampled() => _sampleRate >= 1 || Random.Shared.NextDouble() < _sampleRate;

    private static OrmLogLevel Raise(OrmLogLevel? current, OrmLogLevel next) =>
        current is { } held && held > next ? held : next;

    private List<LoggedParameter> Describe(DbParameterCollection parameters)
    {
        var described = new List<LoggedParameter>(parameters.Count);
        foreach (DbParameter parameter in parameters)
            described.Add(new LoggedParameter(parameter.ParameterName, ParameterRedactor.Describe(parameter.Value, _parameterMaxLength)));
        return described;
    }
}

internal static class DbCommandDiagnostics
{
    internal static Task<int> RunNonQueryAsync(this DbCommand command, StatementKind kind, CancellationToken ct = default) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunAsync(command, kind, lease, static (c, t) => c.ExecuteNonQueryAsync(t), static n => n, ct)
            : command.ExecuteNonQueryAsync(ct);

    internal static Task<object?> RunScalarAsync(this DbCommand command, StatementKind kind, CancellationToken ct = default) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunAsync(command, kind, lease, static (c, t) => c.ExecuteScalarAsync(t), static _ => null, ct)
            : command.ExecuteScalarAsync(ct);

    internal static Task<DbDataReader> RunReaderAsync(
        this DbCommand command, StatementKind kind, CancellationToken ct = default, CommandBehavior behavior = CommandBehavior.Default) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunAsync(command, kind, lease, (c, t) => c.ExecuteReaderAsync(behavior, t), static _ => null, ct)
            : command.ExecuteReaderAsync(behavior, ct);

    // A maintenance statement: one event of kind Maintenance that names its operation.
    internal static Task<DbDataReader> RunMaintenanceReaderAsync(
        this DbCommand command, MaintenanceOperation operation, CancellationToken ct) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunAsync(command, StatementKind.Maintenance, lease,
                static (c, t) => c.ExecuteReaderAsync(t), static _ => null, ct, operation.ToString())
            : command.ExecuteReaderAsync(ct);

    internal static Task<int> RunGraphSaveNonQueryAsync(this DbCommand command, CancellationToken ct) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunGraphSaveAsync(new GraphSaveDiagnosticRun(command, StatementKind.Write, lease),
                () => command.ExecuteNonQueryAsync(ct), static n => n)
            : command.ExecuteNonQueryAsync(ct);

    internal static Task<object?> RunGraphSaveScalarAsync(this DbCommand command, CancellationToken ct) =>
        Lease(command) is { } lease
            ? lease.Diagnostics.RunGraphSaveAsync(new GraphSaveDiagnosticRun(command, StatementKind.Read, lease),
                () => command.ExecuteScalarAsync(ct), static _ => null)
            : command.ExecuteScalarAsync(ct);

    private static readonly ConcurrentDictionary<Type, bool> s_connectionless = new();

    private static GatedDataSource.Lease? Lease(DbCommand command) =>
        OrmDiagnostics.Possible ? GatedDataSource.LeaseOf(ConnectionOf(command)) : null;

    private static DbConnection? ConnectionOf(DbCommand command)
    {
        var type = command.GetType();
        if (s_connectionless.ContainsKey(type)) return null;
        try
        {
            return command.Connection;
        }
        catch (NotSupportedException)
        {
            s_connectionless[type] = true;
            return null;
        }
    }
}

internal sealed class TransactionTrace : IDisposable
{
    private readonly GatedDataSource.Lease _lease;

    private readonly long _start = Stopwatch.GetTimestamp();

    private int _statements;

    private int _finished;

    private TransactionTrace(GatedDataSource.Lease lease)
    {
        _lease = lease;
        Id = OrmDiagnostics.NextId();
    }

    internal long Id { get; }

    internal static TransactionTrace? Begin(DbConnection connection)
    {
        if (!OrmDiagnostics.Possible || GatedDataSource.LeaseOf(connection) is not { Diagnostics.Active: true } lease) return null;
        var trace = new TransactionTrace(lease);
        lease.Transaction = trace;
        trace.Emit(TransactionPhase.Begin);
        return trace;
    }

    internal void Count() => Interlocked.Increment(ref _statements);

    internal void Commit() => Finish(TransactionPhase.Commit);

    internal void Abandon()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return;
        if (ReferenceEquals(_lease.Transaction, this)) _lease.Transaction = null;
    }

    public void Dispose() => Finish(TransactionPhase.Rollback);

    private void Finish(TransactionPhase phase)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0) return;
        if (ReferenceEquals(_lease.Transaction, this)) _lease.Transaction = null;
        Emit(phase);
    }

    private void Emit(TransactionPhase phase) =>
        _lease.Diagnostics.Emit(new TransactionEvent(
            OrmLogLevel.None, _lease.Diagnostics.Provider, phase, _lease.ConnectionId, Id,
            Stopwatch.GetElapsedTime(_start), Volatile.Read(ref _statements)));
}
