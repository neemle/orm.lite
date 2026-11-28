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

    // D6 steps 4 to 9 for one statement.
    private async Task<TResult> RunAsync<TResult>(MaintenanceStep<TResult> step, MaintenanceBudget budget, CancellationToken ct)
    {
        var source = _context.DataSource;
        var lease = new MaintenanceWriteLease(true, null);
        if (step.TakesGate)
        {
            RefuseHolder(step.Port, step.Operation);
            if (budget.IsSpent) return step.Busy();
            lease = await step.Port.TryEnterWriteAsync(source, budget.Remaining, ct);
            if (!lease.Entered) return step.Busy();
        }
        try
        {
            using (DbLane.Enter(DbLaneKind.Ops))
            {
                if (budget.IsSpent) return step.Busy();
                if (await OpenWithinAsync(source, step.Port, budget, ct) is not { } opened) return step.Busy();
                await using var connection = opened;
                var guard = step.Port.RenderLockGuard(budget.Remaining);
                try
                {
                    if (guard is not null) await RunSessionAsync(connection, guard.Setup, budget, ct);
                    await using var command = connection.CreateCommand();
                    command.CommandText = step.Text;
                    // With a server-side lock timeout set, the command timeout gets a second of grace so the lock
                    // timeout fires first and is reported as contention, not as a command timeout.
                    command.CommandTimeout = budget.CommandSeconds(guard is null ? 0 : GuardGraceSeconds);
                    await using var reader = await command.RunMaintenanceReaderAsync(step.Operation, ct);
                    return await step.Read(reader, ct);
                }
                catch (DbException failure) when (step.Port.IsContention(failure))
                {
                    return step.Busy();
                }
                finally
                {
                    if (guard is not null) await TearDownAsync(connection, guard.Teardown);
                }
            }
        }
        finally
        {
            if (lease.Release is { } release) await release.DisposeAsync();
        }
    }

    // ADR D12.2: opening the connection spends the same budget. The budget running out while the caller's token is live,
    // or a failure the provider calls contention, is null: the caller reports Busy. Anything else propagates.
    private static async Task<DbConnection?> OpenWithinAsync(
        DbDataSource source, IMaintenanceProvider port, MaintenanceBudget budget, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(budget.Remaining);
        try
        {
            return await source.OpenConnectionAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return null;
        }
        catch (DbException failure) when (!ct.IsCancellationRequested && port.IsContention(failure))
        {
            return null;
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
