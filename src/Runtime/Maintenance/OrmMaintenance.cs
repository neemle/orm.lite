using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// Explicit database maintenance for one <see cref="OrmContext"/>: one method per operation, each taking a request whose
/// mode is required. Every operation runs in the same order: validate the request, honour cancellation, refuse an
/// unsupported operation or option, refuse to take a writer gate the calling flow already holds, take the gate where
/// the operation writes, then run one statement on its own Ops-lane connection outside any transaction, and release
/// everything on success, failure and cancellation. Contention is a returned status, never an exception.
/// </summary>
public sealed partial class OrmMaintenance
{
    // The second of grace a lock-guarded statement gets over the budget, and all a guard's teardown gets.
    private const int GuardGraceSeconds = 1;

    private readonly OrmContext _context;

    internal OrmMaintenance(OrmContext context) => _context = context;

    private string ProviderId => _context.Provider.Id;

    private static void ValidateMode<TMode>(TMode mode, string name) where TMode : struct, Enum
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(name, mode, "the mode is not one of the defined " + typeof(TMode).Name + " values; name one explicitly.");
    }

    private static void ValidateTimeout(TimeSpan? timeout)
    {
        if (timeout is { } given && given <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException("Timeout", given, "a maintenance timeout must be positive; leave it null for the provider's maintenance timeout.");
    }

    // D6 step 3: the operation's capability first, then the option's. A provider without the port is refused by the
    // operation's capability, before any connection opens.
    private IMaintenanceProvider Require(Capability operation, string node)
    {
        if (_context.Provider is not IMaintenanceProvider port || _context.Provider.Capabilities.Of(operation) == Support.None)
            throw new CapabilityNotSupportedException(ProviderId, node, operation);
        return port;
    }

    private void RequireOption(Capability option, string node)
    {
        if (_context.Provider.Capabilities.Of(option) == Support.None)
            throw new CapabilityNotSupportedException(ProviderId, node, option);
    }

    // A target named by a generated descriptor must be a table: a view has no statistics, indexes or pages of its own.
    private static void RefuseView(EntityDescriptor? table, string operation)
    {
        if (table is { IsView: true })
            throw new ArgumentException(operation + " targets tables; '" + table.Table + "' is a view.", "Table");
    }

    private MaintenanceBudget Budget(IMaintenanceProvider port, TimeSpan? timeout) => new(timeout ?? port.MaintenanceTimeout);

    // D6 step 4: a gate-taking operation called from a flow whose open UnitOfWork holds that gate would wait on itself.
    private void RefuseHolder(IMaintenanceProvider port, MaintenanceOperation operation)
    {
        if (port.WriterKeyOf(_context.DataSource) is not { } key || !WriterFlow.Holds(key)) return;
        throw new InvalidOperationException(
            "an open UnitOfWork in this flow holds the writer gate of database '" + DatabaseName(key) + "', so "
            + operation + " would wait on it; commit or dispose the UnitOfWork and run maintenance outside it.");
    }

    // Only the last segment of the key: a file name, a shared in-memory name or a database name, never a directory,
    // host or credential.
    private static string DatabaseName(string key) => key[(key.LastIndexOfAny(['/', '\\', ':']) + 1)..];

    // ADR D12.4(a): once the caller's token is cancelled, cancellation wins over every other outcome of the run, a
    // finished one included: a result of any status or any failure surfaces as OperationCanceledException carrying that
    // token, with the failure, or the contention or deadline behind a Busy, as its inner exception. Contention is Busy
    // only while the token is live.
    private async Task<TResult> RunAsync<TResult>(MaintenanceStep<TResult> step, MaintenanceBudget budget, CancellationToken ct)
        where TResult : IMaintenanceResult
    {
        TResult result;
        Exception? cause;
        try
        {
            (result, cause) = await RunStepsAsync(step, budget, ct);
        }
        catch (Exception failure) when (ct.IsCancellationRequested && !IsCancellationOf(failure, ct))
        {
            throw new OperationCanceledException(Cancelled, failure, ct);
        }
        if (ct.IsCancellationRequested) throw new OperationCanceledException(Cancelled, cause, ct);
        return result;
    }

    private const string Cancelled = "the maintenance call was cancelled.";

    private static bool IsCancellationOf(Exception failure, CancellationToken ct) =>
        failure is OperationCanceledException cancelled && cancelled.CancellationToken == ct;

    // D6 steps 4 to 9 for one statement. A Busy result carries its cause when there is one: the contention failure of
    // the open or of the statement, or the deadline that ended the open. A gate miss or a spent budget has none.
    private async Task<(TResult Result, Exception? Cause)> RunStepsAsync<TResult>(
        MaintenanceStep<TResult> step, MaintenanceBudget budget, CancellationToken ct)
    {
        var source = _context.DataSource;
        var lease = new MaintenanceWriteLease(true, null);
        if (step.TakesGate)
        {
            RefuseHolder(step.Port, step.Operation);
            if (budget.IsSpent) return (step.Busy(), null);
            lease = await step.Port.TryEnterWriteAsync(source, budget.Remaining, ct);
            if (!lease.Entered) return (step.Busy(), null);
        }
        try
        {
            TResult result;
            Exception? cause = null;
            using (DbLane.Enter(DbLaneKind.Ops))
            {
                if (budget.IsSpent) return (step.Busy(), null);
                var (opened, refused) = await OpenWithinAsync(source, step.Port, budget, ct);
                if (opened is null) return (step.Busy(), refused);
                await using var connection = opened;
                // ADR D12.2/D12.3: an open that succeeds after the budget ran out starts nothing.
                if (budget.IsSpent) return (step.Busy(), null);
                var guard = step.Port.RenderLockGuard(budget.Remaining);
                try
                {
                    if (guard is not null) await RunSessionAsync(connection, guard.Setup, budget, ct);
                    var text = step.Lookup is { } lookup
                        ? lookup.Render(await LookUpAsync(connection, lookup.Text, budget, guard is null ? 0 : GuardGraceSeconds, ct))
                        : step.Text!;
                    // A guard setup or a lookup that spent the budget starts no statement; the guard is still torn down.
                    if (budget.IsSpent) return (step.Busy(), null);
                    await using var command = connection.CreateCommand();
                    command.CommandText = text;
                    // With a server-side lock timeout set, the command timeout gets a second of grace so the lock
                    // timeout fires first and is reported as contention, not as a command timeout. Without one, a
                    // provider with a retry slice waits one slice at a time (D12.4(b)).
                    var slice = guard is null ? step.Port.ContentionRetrySlice : null;
                    command.CommandTimeout = budget.CommandSeconds(guard is null ? 0 : GuardGraceSeconds, slice);
                    await using var reader = await command.RunMaintenanceReaderAsync(step.Operation,
                        slice is { } each ? failure => RetriesContention(command, step.Port, failure, each, budget, ct) : null, ct);
                    result = await step.Read(reader, ct);
                }
                catch (DbException failure) when (!ct.IsCancellationRequested && step.Port.IsContention(failure))
                {
                    result = step.Busy();
                    cause = failure;
                }
                finally
                {
                    if (guard is not null) await TearDownAsync(connection, guard.Teardown);
                }
            }
            return (result, cause);
        }
        finally
        {
            if (lease.Release is { } release) await release.DisposeAsync();
        }
    }

    // ADR D12.4(b): contention is waited out one slice at a time on the same connection, inside the one diagnosed
    // execution, while the budget lasts and the caller has not cancelled; then it is the caller's to report.
    private static bool RetriesContention(
        DbCommand command, IMaintenanceProvider port, DbException failure, TimeSpan slice, MaintenanceBudget budget, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || budget.IsSpent || !port.IsContention(failure)) return false;
        command.CommandTimeout = budget.CommandSeconds(0, slice);
        return true;
    }

    // ADR D12.2: opening the connection spends the same budget. The budget running out while the caller's token is live,
    // or a failure the provider calls contention, is no connection: the caller reports Busy, keeping that exception as
    // its cause (D12.4(a)). Anything else propagates.
    private static async Task<(DbConnection? Connection, Exception? Cause)> OpenWithinAsync(
        DbDataSource source, IMaintenanceProvider port, MaintenanceBudget budget, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(budget.TimerDelay);
        try
        {
            return (await source.OpenConnectionAsync(deadline.Token), null);
        }
        catch (OperationCanceledException expired) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return (null, expired);
        }
        catch (DbException failure) when (!ct.IsCancellationRequested && port.IsContention(failure))
        {
            return (null, failure);
        }
    }

    // Session plumbing rendered from a time span only: it runs on the maintenance connection without a diagnostics
    // event, so a call still reports exactly one statement.
    private static async Task RunSessionAsync(DbConnection connection, string text, MaintenanceBudget budget, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        command.CommandTimeout = budget.CommandSeconds(0);
        await command.ExecuteNonQueryAsync(ct);
    }

    // ADR D12.5: provider plumbing rendered from a descriptor only, run after the guard on the operation's connection and,
    // like the guard, not reported to diagnostics. It answers one value.
    private static async Task<string?> LookUpAsync(DbConnection connection, string text, MaintenanceBudget budget, int grace, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = text;
        command.CommandTimeout = budget.CommandSeconds(grace);
        return await command.ExecuteScalarAsync(ct) as string;
    }

    // A teardown gets one second (the guard grace) and a failure is swallowed: the connection is disposed next, and the
    // provider's reset on close is the second line.
    private static async Task TearDownAsync(DbConnection connection, string text)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = text;
            command.CommandTimeout = GuardGraceSeconds;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (Exception)
        {
        }
    }
}
