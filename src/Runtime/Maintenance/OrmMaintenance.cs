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
    private readonly OrmContext _context;

    private readonly TimeProvider _clock;

    internal OrmMaintenance(OrmContext context)
        : this(context, TimeProvider.System)
    {
    }

    // The production clock made explicit, per instance (ADR D14.2); tests pass their own.
    internal OrmMaintenance(OrmContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    private string ProviderId => _context.Provider.Id;

    // One execution per public call (ADR D14): it owns the token, the one budget (the request's timeout, or the
    // provider's maintenance timeout), the outcome and the cleanup.
    private MaintenanceExecution Execution(IMaintenanceProvider port, TimeSpan? timeout, CancellationToken ct) =>
        new(_context, port, timeout ?? port.MaintenanceTimeout, ct, _clock);

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
}
