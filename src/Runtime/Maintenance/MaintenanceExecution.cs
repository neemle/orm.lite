using System.Data.Common;
using System.Runtime.ExceptionServices;

namespace Neemle.OrmLite;

/// <summary>
/// One public maintenance call (ADR D14): the only place in maintenance that awaits, opens, dispatches or reads. It
/// owns the caller's token, the one budget, the outcome envelope and the cleanup stack, and runs every effect under one
/// rule: capture, arbitrate, admit, dispatch; publish only at the root.
/// <list type="bullet">
/// <item>C1: one execution per public call; a composite's children run inside it and never publish.</item>
/// <item>C2: each new effect is admitted immediately before its dispatch, after its preparation.</item>
/// <item>C3: an effect's resource is registered, or its failure captured, before anything else; a failure is
/// classified once, never when the caller already cancelled.</item>
/// <item>C4: one retry loop, around statement attempts only.</item>
/// <item>C5: the budget is admission only; cancellation is arbitrated after every await and again at the root.</item>
/// <item>C6: a LIFO cleanup stack that never reads or changes the outcome.</item>
/// <item>C7: only the root publishes, after all cleanup.</item>
/// <item>C8: one clock and one latched deadline.</item>
/// </list>
/// Guarantees hold relative to the provider and runtime contracts: a provider call that returns has done what its
/// contract says.
/// </summary>
internal sealed partial class MaintenanceExecution
{
    private const string Cancelled = "the maintenance call was cancelled.";

    // The second of grace a lock-guarded statement and its lookup get over the budget, so the server's lock timeout
    // fires first and is reported as contention; and all a guard's teardown gets.
    private const int GuardGraceSeconds = 1;

    private readonly OrmContext _context;

    private readonly IMaintenanceProvider _port;

    private readonly CancellationToken _caller;

    private readonly Deadline _deadline;

    // The contention behind the effect not yet resolved: set when a failure is classified as contention, cleared when a
    // statement attempt succeeds. A stop reports it as its cause.
    private ExceptionDispatchInfo? _pending;

    // How the statement loop settled the native failure it rethrew through diagnostics, so it is classified once.
    private Stop? _statementStop;

    internal MaintenanceExecution(
        OrmContext context, IMaintenanceProvider port, TimeSpan budget, CancellationToken caller, TimeProvider clock)
    {
        _context = context;
        _port = port;
        _caller = caller;
        _deadline = new Deadline(budget, clock, caller);
    }

    internal Task<TResult> RunAsync<TResult>(MaintenanceStep<TResult> step) where TResult : IMaintenanceResult =>
        RunRootAsync(() => RunStepAsync(step));

    // ADR D5/D6 composite (134 on a provider that checkpoints): a Truncate checkpoint, then VACUUM only when it
    // completed or the database has no WAL. The checkpoint's gate is released before VACUUM is admitted; both spend the
    // one budget.
    internal Task<VacuumResult> RunVacuumAsync(
        MaintenanceStep<WalCheckpointResult>? checkpoint,
        Func<WalCheckpointResult?, MaintenanceStep<VacuumResult>> vacuum,
        Func<MaintenanceStatus, WalCheckpointResult?, VacuumResult> project) =>
        RunRootAsync(() => RunCompositeAsync(checkpoint, vacuum, project));

    private async Task<TResult> RunRootAsync<TResult>(Func<Task<Outcome<TResult>>> run)
    {
        Outcome<TResult> outcome;
        try
        {
            outcome = await run();
        }
        catch (Exception failure)
        {
            outcome = Outcome<TResult>.Failed(ExceptionDispatchInfo.Capture(failure));
        }
        finally
        {
            _deadline.Dispose();
        }
        return Publish(outcome);
    }

    // C7: the final read of the caller's token is the call's linearization point. Once it is cancelled, every outcome
    // is OperationCanceledException carrying it, with the failure or the cause of a Busy inside; an exception that
    // already carries that token is rethrown as it is.
    private TResult Publish<TResult>(Outcome<TResult> outcome)
    {
        if (_caller.IsCancellationRequested)
        {
            if (outcome.Cause?.SourceException is OperationCanceledException own && own.CancellationToken == _caller)
                outcome.Cause.Throw();
            throw new OperationCanceledException(Cancelled, outcome.Cause?.SourceException, _caller);
        }
        if (outcome.Kind == OutcomeKind.Failure) outcome.Cause!.Throw();
        return outcome.Value!;
    }

    private async Task<Outcome<VacuumResult>> RunCompositeAsync(
        MaintenanceStep<WalCheckpointResult>? checkpoint,
        Func<WalCheckpointResult?, MaintenanceStep<VacuumResult>> vacuum,
        Func<MaintenanceStatus, WalCheckpointResult?, VacuumResult> project)
    {
        if (checkpoint is null) return await RunStepAsync(vacuum(null));
        RefuseHolder(MaintenanceOperation.Vacuum);
        var first = await RunStepAsync(checkpoint);
        return first.Kind switch
        {
            OutcomeKind.Failure => Outcome<VacuumResult>.Failed(first.Cause!),
            // A checkpoint that reported no row (its gate was busy, or contention): Busy, no checkpoint to carry.
            OutcomeKind.Busy => Outcome<VacuumResult>.Busy(project(MaintenanceStatus.Busy, null), first.Cause),
            _ when !OrmMaintenance.CheckpointLetsVacuumRun(first.Value!) =>
                Outcome<VacuumResult>.Of(project(MaintenanceStatus.NotRun, first.Value)),
            _ => await RunStepAsync(vacuum(first.Value)),
        };
    }

    // One statement with its fixed stage order, then its cleanup. Every failure is captured into the outcome; the step
    // never publishes.
    private async Task<Outcome<TResult>> RunStepAsync<TResult>(MaintenanceStep<TResult> step)
        where TResult : IMaintenanceResult
    {
        _pending = null;
        var frame = new ResourceFrame();
        Outcome<TResult> outcome;
        try
        {
            outcome = await RunStagesAsync(step, frame);
        }
        catch (Exception failure)
        {
            outcome = Outcome<TResult>.Failed(ExceptionDispatchInfo.Capture(failure));
        }
        await UnwindAsync(frame);
        return outcome;
    }

    // [gate] -> open -> [guard SET] -> [lookup] -> statement attempts -> rows. The text is rendered first, before any
    // I/O.
    private async Task<Outcome<TResult>> RunStagesAsync<TResult>(MaintenanceStep<TResult> step, ResourceFrame frame)
        where TResult : IMaintenanceResult
    {
        var text = step.Render?.Invoke();
        if (step.TakesGate && await EnterGateAsync(step, frame) is { } busy) return busy;
        // A dedicated Ops-lane connection: a UnitOfWork's connection is never reused, and no transaction is begun.
        using var lane = DbLane.Enter(DbLaneKind.Ops);
        var open = await RunEffectAsync(
            () => Admit(deadline: true),
            allowance => _context.DataSource.OpenConnectionAsync(allowance.Token).AsTask(),
            frame.Push,
            deadline: true);
        if (open.Halted is { } unopened) return Stopped(step, unopened);
        return await RunOnConnectionAsync(step, new Session(open.Value, frame), text);
    }

    // D6 steps 4 and 5: the holder refusal, then the writer gate, waited for with the caller's token and the admitted
    // remainder as its timeout. Null once entered; otherwise how the step stops (a gate that timed out is a cause-less
    // Busy).
    private async Task<Outcome<TResult>?> EnterGateAsync<TResult>(MaintenanceStep<TResult> step, ResourceFrame frame)
        where TResult : IMaintenanceResult
    {
        RefuseHolder(step.Operation);
        var gate = await RunEffectAsync(
            () => Admit(),
            allowance => step.Port
                .TryEnterWriteAsync(_context.DataSource, allowance.Remaining, allowance.Token)
                .AsTask(),
            lease => frame.Push(lease.Release));
        if (gate.Halted is { } halted) return Stopped(step, halted);
        return gate.Value.Entered ? null : Outcome<TResult>.Busy(step.Busy(), null);
    }

    // On the open connection: [guard SET] -> [lookup] -> the statement.
    private async Task<Outcome<TResult>> RunOnConnectionAsync<TResult>(
        MaintenanceStep<TResult> step, Session session, string? text) where TResult : IMaintenanceResult
    {
        var guard = step.Port.RenderLockGuard(_deadline.Remaining);
        if (guard is not null)
        {
            // The teardown is registered before the setup is dispatched, so a setup that fails is still torn down.
            var set = await RunSessionCommandAsync(
                session,
                guard.Setup,
                command => command.ExecuteNonQueryAsync(_caller),
                () => session.Frame.PushReset(session.Connection, guard.Teardown));
            if (set.Halted is { } unset) return Stopped(step, unset);
            session = session with { Grace = GuardGraceSeconds };
        }
        if (step.Lookup is { } lookup)
        {
            var answer = await RunSessionCommandAsync(
                session, lookup.Text, command => command.ExecuteScalarAsync(_caller));
            if (answer.Halted is { } unanswered) return Stopped(step, unanswered);
            text = lookup.Render(answer.Value as string);
        }
        // With a server-side lock timeout set, the lock wait is the server's to bound; without one, a provider with a
        // retry slice waits one slice at a time (D12.4(b)).
        var slice = guard is null ? step.Port.ContentionRetrySlice : null;
        return await RunStatementAsync(step, session with { Slice = slice }, text!);
    }

    // The operation statement, through the one diagnostics event, then its rows. The first attempt is admitted right
    // before the event starts; a statement that is never dispatched is no event.
    private async Task<Outcome<TResult>> RunStatementAsync<TResult>(
        MaintenanceStep<TResult> step, Session session, string text) where TResult : IMaintenanceResult
    {
        var command = session.Connection.CreateCommand();
        session.Frame.Push(command);
        command.CommandText = text;
        if (AdmitCommand(command, session) is not { } first) return Stopped(step, Halt());
        _statementStop = null;
        DbDataReader reader;
        try
        {
            reader = await command.RunMaintenanceReaderAsync(
                step.Operation, (statement, _) => ExecuteStatementAsync(statement, first, session), _caller);
        }
        catch (Exception failure)
        {
            return Stopped(step, _statementStop ?? Settle(failure));
        }
        if (_caller.IsCancellationRequested) return Stopped(step, Halt());
        return await ConsumeAsync(step, reader);
    }

    // C4: the one retry loop, around statement attempts only, inside the one diagnostics event. Only a classified
    // contention is retried, only with a retry slice while the caller is live, and every retry is admitted first. A
    // stop rethrows the native failure, so the event's error is native; how it settled is kept, so it is classified
    // once.
    private async Task<DbDataReader> ExecuteStatementAsync(DbCommand command, Allowance allowance, Session session)
    {
        while (true)
        {
            try
            {
                var reader = await command.ExecuteReaderAsync(allowance.Token);
                session.Frame.Push(reader);
                _pending = null;
                return reader;
            }
            catch (Exception failure)
            {
                _statementStop = Settle(failure);
                if (session.Slice is null || !_statementStop.Value.Contended || _caller.IsCancellationRequested) throw;
                if (AdmitCommand(command, session) is not { } next)
                {
                    _statementStop = Halt();
                    throw;
                }
                allowance = next;
            }
        }
    }

    // C5: the executor owns every read and hands each row to the synchronous reducer. An admitted statement keeps its
    // result however long reading it takes; the caller's token is arbitrated after every read.
    private async Task<Outcome<TResult>> ConsumeAsync<TResult>(MaintenanceStep<TResult> step, DbDataReader reader)
        where TResult : IMaintenanceResult
    {
        var rows = step.Rows();
        while (true)
        {
            bool more;
            try
            {
                more = await reader.ReadAsync(_caller);
            }
            catch (Exception failure)
            {
                return Stopped(step, Settle(failure));
            }
            if (_caller.IsCancellationRequested) return Stopped(step, Halt());
            if (!more || !rows.Accept(reader)) return Outcome<TResult>.Of(rows.Complete());
        }
    }

    // A session command (the guard's setup, the schema lookup): plumbing rendered from a time span or a descriptor, run
    // on the operation's connection without a diagnostics event. `admitted` runs after admission, right before
    // dispatch.
    private Task<Effect<TValue>> RunSessionCommandAsync<TValue>(
        Session session, string text, Func<DbCommand, Task<TValue>> dispatch, Action? admitted = null)
    {
        var command = session.Connection.CreateCommand();
        session.Frame.Push(command);
        command.CommandText = text;
        return RunEffectAsync(() => AdmitCommand(command, session), _ =>
        {
            admitted?.Invoke();
            return dispatch(command);
        }, static _ => { });
    }

    // C2 and C3 for one effect: admitted immediately before its dispatch; its resource registered, or its failure
    // settled, before anything else; then the caller's token is arbitrated again.
    private async Task<Effect<TValue>> RunEffectAsync<TValue>(
        Func<Allowance?> admit, Func<Allowance, Task<TValue>> dispatch, Action<TValue> keep, bool deadline = false)
    {
        if (admit() is not { } allowance) return new(default!, Halt());
        TValue value;
        try
        {
            value = await dispatch(allowance);
        }
        catch (Exception failure)
        {
            return new(default!, Settle(failure, deadline));
        }
        keep(value);
        return _caller.IsCancellationRequested ? new(default!, Halt()) : new(value, null);
    }

    // C2: a cancelled caller or a spent budget admits nothing. A command timeout is computed only from the allowance
    // admitted here, so its one-second floor never applies to a spent budget. Only the open gets the deadline's token.
    private Allowance? Admit(int grace = 0, TimeSpan? slice = null, bool deadline = false)
    {
        if (_caller.IsCancellationRequested || _deadline.Admit() is not { } remaining) return null;
        var wait = slice is { } most && most < remaining ? most : remaining;
        var seconds = (int)Math.Max(1, Math.Ceiling(wait.TotalSeconds)) + grace;
        return new Allowance(remaining, seconds, deadline ? _deadline.Token : _caller);
    }

    // C2 for a command: its timeout is computed from this admission, and the admission is confirmed once more after the
    // timeout's setter, immediately before the dispatch.
    private Allowance? AdmitCommand(DbCommand command, Session session)
    {
        if (Admit(session.Grace, session.Slice) is not { } allowance) return null;
        command.CommandTimeout = allowance.CommandTimeoutSeconds;
        return Admit(session.Grace, session.Slice) is null ? null : allowance;
    }

    // C3: a captured failure is classified once. When the caller already cancelled it is not classified: it is the
    // failure (a cause-less cancellation keeps the contention still pending instead). Otherwise contention, or the
    // open's deadline, is Busy with that failure as its cause; anything else is the failure, unchanged.
    private Stop Settle(Exception failure, bool deadline = false)
    {
        if (_caller.IsCancellationRequested)
            return failure is OperationCanceledException { InnerException: null } && _pending is not null
                ? Halt()
                : new Stop(true, ExceptionDispatchInfo.Capture(failure));
        if (!IsBusy(failure, deadline)) return new Stop(true, ExceptionDispatchInfo.Capture(failure));
        _pending = ExceptionDispatchInfo.Capture(failure);
        return new Stop(false, _pending, Contended: true);
    }

    // The one classification: contention by the provider's word, or the open's deadline once it has latched.
    private bool IsBusy(Exception failure, bool deadline) =>
        failure is DbException db
            ? _port.IsContention(db)
            : deadline && failure is OperationCanceledException && _deadline.IsExpired;

    // A stop by admission or by the caller's token: Busy with the pending cause, published as cancellation when the
    // token is cancelled.
    private Stop Halt() => new(false, _pending);

    private static Outcome<TResult> Stopped<TResult>(MaintenanceStep<TResult> step, Stop stop)
        where TResult : IMaintenanceResult =>
        stop.Failed ? Outcome<TResult>.Failed(stop.Cause!) : Outcome<TResult>.Busy(step.Busy(), stop.Cause);

    // D6 step 4: a gate-taking operation called from a flow whose open UnitOfWork holds that gate would wait on itself.
    private void RefuseHolder(MaintenanceOperation operation)
    {
        if (_port.WriterKeyOf(_context.DataSource) is not { } key || !WriterFlow.Holds(key)) return;
        throw new InvalidOperationException(
            "an open UnitOfWork in this flow holds the writer gate of database '" + DatabaseName(key) + "', so "
            + operation + " would wait on it; commit or dispose the UnitOfWork and run maintenance outside it.");
    }

    // Only the last segment of the key: a file name, a shared in-memory name or a database name, never a directory,
    // host or credential.
    private static string DatabaseName(string key) => key[(key.LastIndexOfAny(['/', '\\', ':']) + 1)..];

    private enum OutcomeKind
    {
        Value,
        Busy,
        Failure,
    }

    // C1's envelope: a value, Busy (its reported result and, when there is one, its cause), or a failure.
    private readonly record struct Outcome<TResult>(OutcomeKind Kind, TResult? Value, ExceptionDispatchInfo? Cause)
    {
        internal static Outcome<TResult> Of(TResult value) => new(OutcomeKind.Value, value, null);

        internal static Outcome<TResult> Busy(TResult busy, ExceptionDispatchInfo? cause) =>
            new(OutcomeKind.Busy, busy, cause);

        internal static Outcome<TResult> Failed(ExceptionDispatchInfo failure) =>
            new(OutcomeKind.Failure, default, failure);
    }

    // How an effect stopped its step: a failure, or Busy with a cause; Contended when that very failure was just
    // classified as contention, which is the only failure the statement loop retries.
    private readonly record struct Stop(bool Failed, ExceptionDispatchInfo? Cause, bool Contended = false);

    // An effect's returned value, or how it stopped its step.
    private readonly record struct Effect<TValue>(TValue Value, Stop? Halted);

    // What one admission allows: the budget left, the command timeout computed from it, and the token to dispatch with.
    private readonly record struct Allowance(TimeSpan Remaining, int CommandTimeoutSeconds, CancellationToken Token);

    // The step's open connection and cleanup stack, with the grace and the retry slice its commands are timed with.
    private readonly record struct Session(
        DbConnection Connection, ResourceFrame Frame, int Grace = 0, TimeSpan? Slice = null);
}
